using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Azure.AI.OpenAI;
using OpenAI.Assistants;
using AdmissionAgent.Models;

namespace AdmissionAgent.Services;

public record AgentConfig(string Model, string AssistantId);

public class AdmissionAgentService
{
    private readonly AzureOpenAIClient _openAi;
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
        AzureOpenAIClient openAi,
        AgentConfig config,
        ILogger<AdmissionAgentService> logger)
    {
        _openAi = openAi;
        _config = config;
        _logger = logger;
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
            // ── STEP 1: Planner ────────────────────────────────────────────
            steps.Add(new AgentStep("Planner: analysing application", StepStatus.Done));
            var plan = await PlanAsync(form);
            _logger.LogInformation("Plan: {Plan}", plan);

            // ── STEP 2: Tool Executor ──────────────────────────────────────
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
                toolResults["check_eligibility"] = await CheckEligibilityWithPolicyAsync(form);
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

            // ── STEP 3: Synthesiser ────────────────────────────────────────
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
        var client = _openAi.GetChatClient(_config.Model);

        var prompt = string.Join("\n", new[]
        {
            "You are a university admission planner. Given this application, decide which tools to call.",
            "Available tools: validate_documents, check_eligibility, score_application",
            "",
            "Application summary:",
            $"- Qualification: {form.Qualification}, GPA: {form.Gpa}",
            $"- Field: {form.Field}, Institution: {form.Institution}",
            $"- Programme: {form.Programme}",
            $"- Documents: transcript={form.HasTranscript}, passport={form.HasPassport}, references={form.HasReferences}",
            "",
            "Return ONLY a comma-separated list of tool names to call, nothing else.",
            "Example: validate_documents,check_eligibility,score_application"
        });

        var response = await client.CompleteChatAsync(
        [
            new OpenAI.Chat.SystemChatMessage("You are a university admission AI planner. Be concise."),
            new OpenAI.Chat.UserChatMessage(prompt)
        ]);

        return response.Value.Content[0].Text;
    }

    // ── Tool: Validate documents ──────────────────────────────────────────
    private static string ValidateDocuments(ApplicationForm form)
    {
        var issues = new List<string>();
        if (!form.HasTranscript) issues.Add("missing academic transcript");
        if (!form.HasPassport)   issues.Add("missing passport/ID");
        if (!form.HasReferences) issues.Add("missing reference letters");

        return issues.Count == 0
            ? "All required documents are present."
            : $"Missing documents: {string.Join(", ", issues)}.";
    }

    // ── Tool: Check eligibility using Assistants API + policy doc ─────────
    private async Task<string> CheckEligibilityWithPolicyAsync(ApplicationForm form)
    {
#pragma warning disable OPENAI001
        var assistantClient = _openAi.GetAssistantClient();

        var thread = await assistantClient.CreateThreadAsync();

        var message = string.Join("\n", new[]
        {
            "Please check the eligibility of this student application against the admission policy document.",
            "",
            "Applicant details:",
            $"- Name: {form.FirstName} {form.LastName}",
            $"- Programme applied for: {form.Programme}",
            $"- Highest qualification: {form.Qualification}",
            $"- Field of study: {form.Field}",
            $"- GPA: {form.Gpa}",
            $"- Graduation year: {form.GradYear}",
            $"- Institution: {form.Institution}",
            $"- Country: {form.Country}",
            $"- Documents: transcript={form.HasTranscript}, passport={form.HasPassport}, references={form.HasReferences}",
            "",
            "Check:",
            "1. Does the GPA meet the minimum requirement for this programme?",
            "2. Is the prior qualification eligible for entry?",
            "3. Are there any mathematics prerequisite requirements based on the field of study?",
            "4. Should a bridging course be recommended?",
            "5. Are there any country-specific requirements?",
            "",
            "Provide a concise eligibility summary."
        });

        await assistantClient.CreateMessageAsync(thread.Value.Id, MessageRole.User, [
            MessageContent.FromText(message)
        ]);

        var run = await assistantClient.CreateRunAsync(thread.Value.Id, _config.AssistantId);

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (run.Value.Status != RunStatus.Completed && run.Value.Status != RunStatus.Failed)
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Assistant run timed out.");

            await Task.Delay(1500);
            run = await assistantClient.GetRunAsync(thread.Value.Id, run.Value.Id);
        }

        if (run.Value.Status == RunStatus.Failed)
            throw new Exception($"Assistant run failed: {run.Value.LastError?.Message}");

        var messages = assistantClient.GetMessagesAsync(thread.Value.Id);
        await foreach (var msg in messages)
        {
            if (msg.Role == MessageRole.Assistant)
                return msg.Content.FirstOrDefault()?.Text ?? "No eligibility response.";
        }

        return "Could not retrieve eligibility check from policy document.";
    }

    // ── Tool: Score application ───────────────────────────────────────────
    private static string ScoreApplication(ApplicationForm form)
    {
        var score = 0;

        var gpaRaw = form.Gpa.Split('/')[0].Trim();
        if (double.TryParse(gpaRaw, out var gpa))
            score += gpa >= 3.7 ? 40 : gpa >= 3.3 ? 30 : gpa >= 3.0 ? 20 : 5;

        score += form.Qualification == "masters" ? 30 : form.Qualification == "bachelors" ? 25 : 10;
        score += (form.HasTranscript ? 10 : 0) + (form.HasPassport ? 5 : 0) + (form.HasReferences ? 10 : 0);
        score += form.Statement.Length > 50 ? 5 : 0;

        return $"Application score: {score}/100. " +
               (score >= 75 ? "Strong application." : score >= 50 ? "Borderline — may need review." : "Weak application.");
    }

    // ── Synthesiser: produce final human-readable decision ────────────────
    private async Task<Decision> SynthesiseAsync(ApplicationForm form, Dictionary<string, string> toolResults)
    {
        var client = _openAi.GetChatClient(_config.Model);
        var toolSummary = string.Join("\n", toolResults.Select(kv => $"- {kv.Key}: {kv.Value}"));

        var prompt =
            "You are a university admission officer at Navitas College.\n" +
            "Based on the tool results below, make a final admission decision.\n\n" +
            $"Applicant: {form.FirstName} {form.LastName}\n" +
            $"Programme: {form.Programme}\n\n" +
            $"Tool results:\n{toolSummary}\n\n" +
            "Respond with JSON only, no markdown:\n" +
            "{\n" +
            "  \"outcome\": \"approved\" | \"review\" | \"declined\",\n" +
            "  \"summary\": \"2-3 sentence explanation including any bridging course recommendations\"\n" +
            "}";

        var response = await client.CompleteChatAsync(
        [
            new OpenAI.Chat.SystemChatMessage("You are a university admission officer. Respond only with valid JSON."),
            new OpenAI.Chat.UserChatMessage(prompt)
        ]);

        var content = response.Value.Content[0].Text
            .Replace("```json", "")
            .Replace("```", "")
            .Trim();

        var json       = JsonDocument.Parse(content);
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
