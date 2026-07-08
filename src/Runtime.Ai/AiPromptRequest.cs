namespace Runtime.Ai;

// PromptContractId is the governed PCT-EXE-00xx id this call realizes -
// carried through to IEvidenceSink so every AI call is traceable to the
// Prompt Contract that authorized it, matching SkillHost's own
// SubjectId=skillId audit convention.
public sealed record AiPromptRequest(
    string PromptContractId,
    string SystemPrompt,
    string UserPrompt,
    double Temperature = 0.0,
    int MaxOutputTokens = 2000);
