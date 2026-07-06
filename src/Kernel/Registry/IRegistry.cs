namespace Kernel.Registry;

// The Kernel-boundary shape of I4 §8's Registration Contract and Discovery
// read-contract. Public because Construction Units outside the Kernel
// (the future Instance Supervisor, S3; the future Catalog, out of this
// Epic's scope) call it to function — per I4 §7's public/internal rule.
public interface IRegistry
{
    RegisterResult Register(DefinitionSubmission submission);

    QueryResult Query(string id);
}
