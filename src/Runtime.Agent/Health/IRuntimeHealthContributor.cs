namespace Runtime.Agent.Health;

public interface IRuntimeHealthContributor
{
    RuntimeHealthReport Check();
}
