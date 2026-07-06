namespace Runtime.DomainContent.EpicLifecycle;

// An in-memory stand-in for one epics/<name>.md instance's accumulating
// sections, mirroring FeatureWorkspace's shape exactly but adapted to the
// Epic header fields epics/0001-0003 already established (Epic Name /
// Stage / Corresponds To / Milestone / External Tracker Reference) instead
// of features/*.md's Feature Name / Stage / External Tracker Reference.
// No file-system persistence, same as FeatureWorkspace - out of this
// Story's scope.
public sealed class EpicWorkspace
{
    public string Name { get; }

    public string Stage { get; private set; } = "Intake";

    public string? ProblemStatement { get; private set; }

    public string? Motivation { get; private set; }

    public IReadOnlyList<(string Question, string? Answer)> ClarificationLog => _clarificationLog;

    public string? Roadmap { get; private set; }

    public string? Plan { get; private set; }

    private readonly List<(string Question, string? Answer)> _clarificationLog = new();

    public EpicWorkspace(string name) => Name = name;

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
