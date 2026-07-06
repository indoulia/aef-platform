using Runtime.Context;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.FeatureLifecycle;

// The executable realization of SKL-EXE-0004
// (framework/execution/skills/feature-intake-and-clarify.md)'s own Behavior
// steps 1-5: instantiate the template, populate Intake, enumerate the
// ambiguity, escalate if unanswered, or record the answer and advance once
// supplied. Step 3 (attempt resolution via framework/knowledge/) is not
// realized here — no Knowledge & Evidence Store exists yet (Construction
// Unit 5, CU-4, not started); per docs/aep/P6-...md §17, Knowledge
// consultation is advisory-only, never a hard dependency, so its absence
// only means every ambiguity currently escalates rather than sometimes being
// auto-resolved — an accepted, disclosed limitation, not a defect.
public sealed class IntakeAndClarificationBehavior : IBehavior
{
    private const string AmbiguityQuestion = "Which user roles does this feature apply to?";

    private readonly FeatureWorkspace _workspace;
    private readonly string _problemStatement;
    private readonly string _motivation;

    public string? SuppliedAnswer { get; set; }

    public IntakeAndClarificationBehavior(FeatureWorkspace workspace, string problemStatement, string motivation)
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
            return IntakeOutcome.PendingClarification(AmbiguityQuestion);
        }

        _workspace.RecordClarificationAnswer(AmbiguityQuestion, SuppliedAnswer);
        _workspace.AdvanceStage("Clarification");
        return IntakeOutcome.Resolved;
    }
}
