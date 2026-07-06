namespace Runtime.DomainContent.FeatureLifecycle;

// An in-memory stand-in for one features/<name>.md instance's accumulating
// sections (docs/discovery/composition/08-feature-lifecycle.md §3 — "the
// file accumulates sections stage by stage, never rewritten from scratch").
// No file-system or Knowledge-Store persistence is built here — out of this
// Story's scope, deferred to whichever future CU builds real Document
// persistence (not yet named anywhere in CU-1-4).
public sealed class FeatureWorkspace
{
    public string Name { get; }

    public string Stage { get; private set; } = "Intake";

    public string? ProblemStatement { get; private set; }

    public string? Motivation { get; private set; }

    public IReadOnlyList<(string Question, string? Answer)> ClarificationLog => _clarificationLog;

    public string? Roadmap { get; private set; }

    public string? Plan { get; private set; }

    private readonly List<(string Question, string? Answer)> _clarificationLog = new();

    public FeatureWorkspace(string name) => Name = name;

    public void PopulateIntake(string problemStatement, string motivation)
    {
        ProblemStatement = problemStatement;
        Motivation = motivation;
    }

    public void RecordClarificationAnswer(string question, string answer) => _clarificationLog.Add((question, answer));

    public void AdvanceStage(string stage) => Stage = stage;

    public void DraftRoadmap(string roadmap) => Roadmap = roadmap;

    public void PopulatePlan(string plan) => Plan = plan;
}
