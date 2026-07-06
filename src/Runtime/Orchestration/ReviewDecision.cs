namespace Runtime.Orchestration;

// Human-Approval-Gates-table entry #3's own "what the human decides"
// (docs/discovery/orchestration/09-human-approval-gates.md §1): approve or
// reject the Roadmap. Its own type — distinct from a plain supplied
// Clarification string — is what makes "only a recorded Review Decision
// resumes or terminates" a code-level guarantee, not just a documented rule.
public sealed record ReviewDecision(bool Approved, string? Reason);
