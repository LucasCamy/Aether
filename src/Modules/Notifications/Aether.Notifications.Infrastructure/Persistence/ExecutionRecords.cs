namespace Aether.Notifications.Infrastructure.Persistence;

public sealed class InboxRecord
{
    public Guid TenantId { get; set; }
    public Guid MessageId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
public sealed class AttemptRecord
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid DispatchId { get; set; }
    public DateOnly CreatedMonth { get; set; }
    public int Number { get; set; }
    public Guid LeaseToken { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string Outcome { get; set; } = "started";
    public string? ProviderMessageId { get; set; }
    public string? ErrorCode { get; set; }
}
