namespace Runtime.DomainContent.DeliveryExecution;

// The AI Agent's own drafted content for each audience, kept as two
// distinct fields rather than one combined blob - the content itself is
// this Skill's caller's generative work (drafting prose is not
// deterministic logic, the same reasoning that keeps SKL-EXE-0018 from
// writing the code change itself); this Skill's own job is to enforce
// CEI-DEL-06's audience-separation structurally and apply the project's
// own read-gate policy, never to author the prose.
public sealed record DocumentationRequest(
    string StoryReferenceId,
    string EndUserContent,
    string MaintainerContent,
    bool ProjectRequiresHumanReadGate);
