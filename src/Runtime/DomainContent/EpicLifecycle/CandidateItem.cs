namespace Runtime.DomainContent.EpicLifecycle;

// One row of a declared candidate-and-dependency table
// (epics/0004-epic-granularity-autonomous-lifecycle.md Roadmap: "a
// declared candidate table... never authors a new candidate"). DependencyId
// is the Id of the governed artifact whose Active Runtime Catalog entry
// evidences the declared dependency is satisfied.
public sealed record CandidateItem(string Id, string Name, string DependencyId);
