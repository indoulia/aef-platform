namespace Runtime.Agent.Capabilities;

// Hand-authored, one entry per capability id
// EngineeringExecutionPipeline.Dispatch already switches on
// (src/Runtime/Execution/EngineeringExecutionPipeline.cs). Kept in sync
// manually - the same precedent EngineeringArchitectureManifest already
// sets for its own pipeline-coupled table - since there is no richer
// metadata anywhere in the Runtime to reflect this from instead.
// ExecutionMode is always "Synchronous" today: the field is the extension
// point the GA-2 brief asks for ("supported execution modes"), not because
// multiple modes exist yet.
public sealed class EngineeringAgentCapabilityCatalog : ICapabilityCatalog
{
    private static readonly IReadOnlyList<CapabilityDescriptor> Capabilities = new[]
    {
        new CapabilityDescriptor(
            "WFL-DEL-0001", "Start Feature Lifecycle",
            "Starts the Feature Lifecycle Orchestrator for a new feature request.",
            Array.Empty<string>(), "Synchronous"),
        new CapabilityDescriptor(
            "WFL-DEL-0004", "Start Epic Lifecycle",
            "Starts the Epic Lifecycle Orchestrator for a new epic request.",
            Array.Empty<string>(), "Synchronous"),
        new CapabilityDescriptor(
            "WFL-DEL-0005", "Implementation Validation",
            "Runs specification-conformance and architecture-standards validation and synthesizes a merge-readiness signal.",
            new[] { "SpecificationConformanceValidationRequest", "ArchitectureStandardsConformanceReviewRequest" }, "Synchronous"),
        new CapabilityDescriptor(
            "WFL-DEL-0006", "Engineering Planning",
            "Runs Discovery, Specification Drafting, and Readiness Classification against a repository to produce a Business-Approved Specification.",
            new[] { "RepositoryName", "RepositoryRoot" }, "Synchronous"),
        new CapabilityDescriptor(
            "WFL-DEL-0007", "Specification Review",
            "Runs an independent review of a drafted specification.",
            new[] { "SpecificationReviewRequest" }, "Synchronous"),
        new CapabilityDescriptor(
            "WFL-DEL-0008", "Repository Intelligence",
            "Runs Repository & Architecture Understanding, Code Intelligence, optional Diagnostics, and Recommendation Synthesis against a repository.",
            new[] { "RepositoryName", "RepositoryRoot" }, "Synchronous"),
        new CapabilityDescriptor(
            "SKL-EXE-0018", "Delivery Implementation",
            "Executes a governed Delivery Execution implementation request against a repository.",
            new[] { "ImplementationRequest", "RepositoryRoot" }, "Synchronous"),
        new CapabilityDescriptor(
            "REPO-VALIDATION", "Repository Validation",
            "Runs the detected build/test command for a repository and records a verification outcome.",
            new[] { "RepositoryName", "RepositoryRoot" }, "Synchronous"),
        new CapabilityDescriptor(
            "WFL-DEL-0009", "Repository Migration Planning",
            "Classifies a migration/modernization request, drafts a phased strategy and rollback plan, and assembles a Business-Approved Migration Specification. Plans, never executes, transformations.",
            new[] { "RepositoryName", "RepositoryRoot" }, "Synchronous"),
        new CapabilityDescriptor(
            "WFL-DEL-0011", "Repository Maintenance Planning",
            "Assesses a repository's dependency, SDK, package, configuration, structure, build, documentation, and code hygiene health, and assembles a Business-Approved Maintenance Specification decomposed into ordinary Stories. Plans, never executes, changes.",
            new[] { "RepositoryName", "RepositoryRoot" }, "Synchronous"),
        new CapabilityDescriptor(
            "WFL-DEL-0012", "Platform Lifecycle Management",
            "Classifies an Installation/Upgrade/Repair/Reset/Backup/Restore request against AEF's own installation and assembles a Human-Approved Platform Lifecycle Operation Plan. Plans, never executes, the actual operation.",
            new[] { "RepositoryName", "RepositoryRoot" }, "Synchronous"),
        new CapabilityDescriptor(
            "PROMPT-GROUNDING", "Prompt Grounding",
            "Turns a user prompt into a grounded brief for a coding agent: verified repository facts with file and line, where to work, build and test commands, flagged assumptions, open questions, and acceptance checks. Reads only; never modifies the repository.",
            new[] { "UserPrompt", "RepositoryName", "RepositoryRoot" }, "Synchronous"),
        new CapabilityDescriptor(
            "CHANGE-CHECK", "Change Check",
            "Checks a change a coding agent has made: files changed since a base, tests affected, files outside the expected set, build and test result, and each acceptance check marked met, not met, or cannot tell. Optional string arguments: BaseRef (default: merge-base with the default branch), AcceptanceChecks and ExpectedFiles (newline-separated, from the grounded brief), SkipBuildAndTests (\"true\" to skip). Never edits source files; unless skipped it runs the repository build and tests, which execute repository code and write build output.",
            new[] { "RepositoryName", "RepositoryRoot" }, "Synchronous"),
    };

    public IReadOnlyList<CapabilityDescriptor> Discover() => Capabilities;
}
