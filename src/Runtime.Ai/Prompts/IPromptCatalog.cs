namespace Runtime.Ai.Prompts;

public interface IPromptCatalog
{
    PromptTemplate? Get(string promptContractId);
}
