using System.Diagnostics;
using System.Diagnostics.Metrics;
using Azure.AI.OpenAI;
using AdmissionAgent.Models;

namespace AdmissionAgent.Services;

public record AgentConfig(string Model);

public class AdmissionAgentService
{
    private readonly OpenAIClient _openAi;
    private readonly AgentConfig _config;
    private readonly ILogger<AdmissionAgentService> _logger;

    // ── Prometheus metrics ─────────────────────────────────────────────────
    private static readonly Meter _meter = new("AdmissionAgent");
    private static readonly Counter<long> _evaluationsTotal =
        _meter.CreateCounter<long>("admission_evaluations_total", description: "Total admission evaluations");
    private static readonly Counter<long> _agentStepsTotal =
        _meter.CreateCounter<long>("admission_agent_steps_total", description: "Total agent tool steps executed");
    private static readonly Histogram<double> _evaluationDuration =
        _meter.CreateHistogram<double>("admission_evaluation_duration_ms", description: "Evaluation duration in ms");

    public AdmissionAgentService(
        OpenAIClient openAi,
        AgentConfig config,
        ILogger<AdmissionAgentService> logger)
    {
        _openAi  = openAi;
        _config  = config;
        _logger  = logger;
    }

    // ── Entry point ────────────────────────────────────────────────────────
    public async Task<EvaluationResult> EvaluateAsync(ApplicationForm form)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N")[..12];
        var sw      = Stopwatch.StartNew();
        var steps   = new List<AgentStep>();

        _logger.LogInformation("Starting evaluation for {Email}, traceId={TraceId}", form.Email, traceId);
        _evaluationsTotal.Add(1);

        try
        {
            // ── STEP 1: Planner — decide which tools to call ───────────────
            steps.Add(new AgentStep("Planner: analysing application", StepStatus.Done));
            var plan = await PlanAsync(form);
            _logger.LogInformation("Plan: {Plan}", plan);

            // ── STEP 2: Tool Executor — run each tool in the plan ──────────
            var toolResults = new Dictionary<string, string>();

            if (plan.Contains("validate_documents"))
            {
                steps.Add(new AgentStep("Tool: validate documents", StepStatus.Active));
                toolResults["validate_documents"] = ValidateDocuments(form);
                steps[^1] = steps[^1] with { Status = StepStatus.Done };
                _agentStepsTotal.Add(1, new TagList { { "tool", "validate_documents" } });
            }

            if (plan.Contains("check_eligibility"))
            {
                steps.Add(new AgentStep("Tool: check eligibility requirements", StepStatus.Active));
                toolResults["check_eligibility"] = CheckEligibility(form);
                steps[^1] = steps[^1] with { Status = StepStatus.Done };
                _agentStepsTotal.Add(1, new TagList { { "tool", "check_eligibility" } });
            }

            if (plan.Contains("score_application"))
            {
                steps.Add(new AgentStep("Tool: score academic record", StepStatus.Active));
                toolResults["score_application"] = ScoreApplication(form);
                steps[^1] = steps[^1] with { Status = StepStatus.Done };
                _agentStepsTotal.Add(1, new TagList { { "tool", "score_application" } });
            }

            // ── STEP 3: Synthesiser — produce the final decision ───────────
            steps.Add(new AgentStep("Synthesiser: generating decision", StepStatus.Active));
            var decision = await SynthesiseAsync(form, toolResults);
            steps[^1] = steps[^1] with { Status = StepStatus.Done };

            sw.Stop();
            _evaluationDuration.Record(sw.ElapsedMilliseconds);
            _logger.LogInformation("Evaluation complete: {Outcome} in {Ms}ms", decision.Outcome, sw.ElapsedMilliseconds);

            return new EvaluationResult("complete", steps, decision, traceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent evaluation failed");
            steps.Add(new AgentStep($"Error: {ex.Message}", StepStatus.Error));
            return new EvaluationResult("error", steps,
                new Decision(DecisionOutcome.Declined, "An error occurred during evaluation. Please try again."),
                traceId);
        }
    }

    // ── Planner: ask GPT which tools are needed ───────────────────────────
    private async Task<string> PlanAsync(ApplicationForm form)
    {
        var prompt = $"""
            You are a university admission planner. Given this application, decide which tools to call.
            Available tools: validate_documents, check_eligibility, score_application
            
            Application summary:
            - Qualification: {form.Qualification}, GPA: {form.Gpa}
            - Field: {form.Field}, Institution: {form.Institution}
            - Programme: {form.Programme}
            - Documents: transcript={form.HasTranscript}, passport={form.HasPassport}, references={form.HasReferences}
            
            Return ONLY a comma-separated list of tool names to call, nothing else.
            Example: validate_documents,check_eligibility,score_application
            """;

        var response = await _openAi.GetChatCompletionsAsync(
            new ChatCompletionsOptions(_config.Model, [
                new ChatRequestSystemMessage("You are a university admission AI planner. Be concise."),
                new ChatRequestUserMessage(prompt)
            ]) { MaxTokens = 50, Temperature = 0 });

        return response.Value.Choices[0].Message.Content;
    }

    // ── Tool: Validate documents ──────────────────────────────────────────
    private static string ValidateDocuments(ApplicationForm form)
    {
        var issues = new List<string>();
        if (!form.HasTranscript)  issues.Add("missing academic transcript");
        if (!form.HasPassport)    issues.Add("missing passport/ID");
        if (!form.HasReferences)  issues.Add("missing reference letters");

        return issues.Count == 0
            ? "All required documents are present."
            : $"Missing documents: {string.Join(", ", issues)}.";
    }

    // ── Tool: Check eligibility ───────────────────────────────────────────
    private static string CheckEligibility(ApplicationForm form)
    {
        var issues = new List<string>();

        // GPA check — parse "3.7 / 4.0" or "3.7"
        var gpaRaw = form.Gpa.Split('/')[0].Trim();
        if (double.TryParse(gpaRaw, out var gpa) && gpa < 3.0)
            issues.Add($"GPA {gpa:F1} is below the minimum of 3.0");

        // Qualification check
        if (form.Qualification is "diploma" or "other")
            issues.Add("qualification level may not meet entry requirements");

        // Grad year — must not be in the future
        if (int.TryParse(form.GradYear, out var year) && year > DateTime.UtcNow.Year)
            issues.Add("graduation year is in the future");

        return issues.Count == 0
            ? "Applicant meets all eligibility requirements."
            : $"Eligibility issues: {string.Join("; ", issues)}.";
    }

    // ── Tool: Score application ───────────────────────────────────────────
    private static string ScoreApplication(ApplicationForm form)
    {
        var score = 0;

        // GPA
        var gpaRaw = form.Gpa.Split('/')[0].Trim();
        if (double.TryParse(gpaRaw, out var gpa))
            score += gpa >= 3.7 ? 40 : gpa >= 3.3 ? 30 : gpa >= 3.0 ? 20 : 5;

        // Qualification
        score += form.Qualification == "masters" ? 30 : form.Qualification == "bachelors" ? 25 : 10;

        // Documents complete
        score += (form.HasTranscript ? 10 : 0) + (form.HasPassport ? 5 : 0) + (form.HasReferences ? 10 : 0);

        // Statement provided
        score += form.Statement.Length > 50 ? 5 : 0;

        return $"Application score: {score}/100. " +
               (score >= 75 ? "Strong application." : score >= 50 ? "Borderline — may need review." : "Weak application.");
    }

    // ── Synthesiser: produce final human-readable decision ───────────────
    private async Task<Decision> SynthesiseAsync(ApplicationForm form, Dictionary<string, string> toolResults)
    {
        var toolSummary = string.Join("\n", toolResults.Select(kv => $"- {kv.Key}: {kv.Value}"));

        var prompt = $"You are a university admission officer. Based on the tool results below, make a final decision.\n\n" +
            $"Applicant: {form.FirstName} {form.LastName}\n" +
            $"Programme: {form.Programme}\n\n" +
            $"Tool results:\n{toolSummary}\n\n" +
            "Respond with JSON only, no markdown:\n" +
            "{\n" +
            "  \"outcome\": \"approved\" | \"review\" | \"declined\",\n" +
            "  \"summary\": \"2-3 sentence explanation for the applicant\"\n" +
            "}";

        var response = await _openAi.GetChatCompletionsAsync(
            new ChatCompletionsOptions(_config.Model, [
                new ChatRequestSystemMessage("You are a university admission officer. Respond only with JSON."),
                new ChatRequestUserMessage(prompt)
            ]) { MaxTokens = 200, Temperature = 0.2f });

        var content = response.Value.Choices[0].Message.Content;

        // Parse the JSON response
        var json = System.Text.Json.JsonDocument.Parse(content);
        var outcomeStr = json.RootElement.GetProperty("outcome").GetString() ?? "review";
        var summary    = json.RootElement.GetProperty("summary").GetString() ?? "No summary provided.";

        var outcome = outcomeStr switch
        {
            "approved" => DecisionOutcome.Approved,
            "declined" => DecisionOutcome.Declined,
            _          => DecisionOutcome.Review,
        };

        return new Decision(outcome, summary);
    }
}
