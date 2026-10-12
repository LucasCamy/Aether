namespace Aether.Notifications.Infrastructure.Persistence;

public sealed class TenantRecord
{
    public Guid Id { get; set; }
    public string Status { get; set; } = "active";
}
public sealed class DispatchRegistryRecord
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public DateOnly CreatedMonth { get; set; }
}
public sealed class IdempotencyRecord
{
    public Guid TenantId { get; set; }
    public string Key { get; set; } = "";
    public byte[] Hash { get; set; } = [];
    public Guid NotificationId { get; set; }
    public Guid DispatchId { get; set; }
    public DateTimeOffset AcceptedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
public sealed class OutboxRecord
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid DispatchId { get; set; }
    public string Payload { get; set; } = "{}";
    public string RoutingKey { get; set; } = "";
    public string Exchange { get; set; } = "comm.dispatch.v1";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset AvailableAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public int Attempts { get; set; }
}
