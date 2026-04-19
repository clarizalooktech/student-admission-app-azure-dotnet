namespace AdmissionAgent.Models;

// ── Incoming from React form ──────────────────────────────────────────────
public record ApplicationForm(
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string Country,
    string Qualification,
    string Gpa,
    string Field,
    string Institution,
    string GradYear,
    string Programme,
    string StartDate,
    string Statement,
    bool HasTranscript,
    bool HasPassport,
    bool HasReferences
);

// ── Agent loop types ──────────────────────────────────────────────────────
public enum StepStatus { Pending, Active, Done, Error }

public record AgentStep(string Label, StepStatus Status);

public enum DecisionOutcome { Approved, Review, Declined }

public record Decision(DecisionOutcome Outcome, string Summary);

// ── API response back to React ────────────────────────────────────────────
public record EvaluationResult(
    string Status,           // "complete" | "error"
    List<AgentStep> Steps,
    Decision Decision,
    string TraceId           // for the observability pills in the UI
);
