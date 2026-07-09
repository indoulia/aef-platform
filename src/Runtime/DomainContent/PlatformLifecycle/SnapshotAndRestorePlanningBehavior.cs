using Runtime.Context;
using Runtime.Execution;
using Runtime.Hosts.Skill;

namespace Runtime.DomainContent.PlatformLifecycle;

// The executable realization of SKL-EXE-0048. Backup is a named git ref
// plus a manifest - never a new file-copy mechanism (Backup & Restore
// Model). This Behavior never creates a tag or checks out a ref itself
// (Scope Boundary: plans only); it names the repository's own already-known
// current branch (RepositoryExecutionContext, read-only) as the candidate
// ref where known, and honestly requires human input where it is not.
// Restore never re-runs the Repair Model's own integrity check here - it
// only names that check as a required post-restore step, per
// Responsibility 7's own postcondition ("re-running... against the
// restored state" happens after a human has actually restored, not during
// planning).
public sealed class SnapshotAndRestorePlanningBehavior : IBehavior
{
    public object? Execute(RuntimeExecutionContext context)
    {
        if (!context.Tier3SkillContent.TryGetValue("SnapshotAndRestorePlanningRequest", out var raw) || raw is not SnapshotAndRestorePlanningRequest request)
        {
            return new SnapshotAndRestorePlanningResult(Array.Empty<string>(), new[] { "No snapshot and restore planning request was supplied." });
        }

        return request.OperationType switch
        {
            "Backup" => PlanBackup(request.RepositoryContext),
            "Restore" => PlanRestore(request.TargetGitRef),
            _ => new SnapshotAndRestorePlanningResult(Array.Empty<string>(), new[] { $"Unsupported snapshot/restore operation type '{request.OperationType}'." }),
        };
    }

    private static SnapshotAndRestorePlanningResult PlanBackup(RepositoryExecutionContext repositoryContext)
    {
        if (repositoryContext.CurrentBranch is { } branch)
        {
            var details = new[] { $"Candidate backup ref: current branch '{branch}' of '{repositoryContext.RepositoryName}'. Manifest: Governed content and Registry/version state as of this ref." };
            var openItems = repositoryContext.IsWorkingTreeClean == false
                ? new[] { "Working tree is not clean - commit or stash before this ref is a coherent snapshot." }
                : Array.Empty<string>();
            return new SnapshotAndRestorePlanningResult(details, openItems);
        }

        return new SnapshotAndRestorePlanningResult(
            Array.Empty<string>(),
            new[] { "Current branch is unknown - naming a coherent backup ref requires human input." });
    }

    private static SnapshotAndRestorePlanningResult PlanRestore(string? targetGitRef)
    {
        if (string.IsNullOrEmpty(targetGitRef))
        {
            return new SnapshotAndRestorePlanningResult(Array.Empty<string>(), new[] { "No target git ref was supplied - restoring requires human input to name one." });
        }

        var details = new[]
        {
            $"Target ref to check out: '{targetGitRef}'.",
            "Required post-restore step: re-run the Repair Model's own reference/registration integrity check against the restored state before considering this Restore complete.",
        };
        return new SnapshotAndRestorePlanningResult(details, Array.Empty<string>());
    }
}
