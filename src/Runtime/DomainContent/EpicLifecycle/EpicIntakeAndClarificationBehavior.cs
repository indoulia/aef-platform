using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.EpicLifecycle;

// The executable realization of SKL-EXE-0013
// (framework/execution/skills/epic-intake-and-clarify.md)'s Behavior -
// SKL-EXE-0004's own Behavior steps 1-5, adapted to the Epic header shape,
// per epics/0004-...md's S17 acceptance criterion 1. AGT-EXE-0002 and
// SKL-EXE-0004 are not modified or referenced by this class.
public sealed class EpicIntakeAndClarificationBehavior : IBehavior
{
    private const string AmbiguityQuestion = "Which already-declared candidate does this Epic target?";

    private readonly EpicWorkspace _workspace;
    private readonly string _problemStatement;
    private readonly string _motivation;

    public string? SuppliedAnswer { get; set; }

    public EpicIntakeAndClarificationBehavior(EpicWorkspace workspace, string problemStatement, string motivation)
    {
        _workspace = workspace;
        _problemStatement = problemStatement;
        _motivation = motivation;
    }

    public object? Execute(RuntimeExecutionContext context)
    {
        _workspace.PopulateIntake(_problemStatement, _motivation);

        if (SuppliedAnswer is null)
        {
            return EpicIntakeOutcome.PendingClarification(AmbiguityQuestion);
        }

        _workspace.RecordClarificationAnswer(AmbiguityQuestion, SuppliedAnswer);
        _workspace.AdvanceStage("Clarification");
        return EpicIntakeOutcome.Resolved;
    }
}
