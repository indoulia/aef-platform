namespace Runtime.Orchestration;

// D10 §3's Manual-trigger payload shape: names only the target Workflow and
// its first step's input — never a full script of subsequent steps.
public sealed record ManualTrigger(string WorkflowId, IReadOnlyDictionary<string, object>? InitialInput);
