namespace Aether.Tenancy.Domain;

public enum TenantStatus { Active, Suspended, Disabled }
public sealed class Tenant
{
    public Guid Id { get; }
    public TenantStatus Status { get; private set; } = TenantStatus.Active;
    public Tenant(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("Tenant obrigatório.");
        Id = id;
    }
    public void Suspend() => Status = TenantStatus.Suspended;
    public void Disable() => Status = TenantStatus.Disabled;
    public void Activate() => Status = TenantStatus.Active;
}
