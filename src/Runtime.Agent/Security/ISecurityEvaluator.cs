namespace Runtime.Agent.Security;

public interface ISecurityEvaluator
{
    SecurityDecision Evaluate(SecurityContext context);
}
