namespace Kernel.Registry;

// Single member today: the Registry write-side skeleton (S1) only ever
// admits an entry as Active. Deprecated/Superseded/Retired belong to
// later Stories that introduce a transition path away from Active.
public enum RegistryEntryStatus
{
    Active
}
