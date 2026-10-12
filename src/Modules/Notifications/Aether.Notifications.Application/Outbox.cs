namespace Aether.Notifications.Application;

public sealed record OutboxMessage(Guid TenantId, Guid Id, Guid LeaseToken, string Exchange, string RoutingKey, string Payload);
public interface IOutboxStore
{
    Task<OutboxMessage[]> ClaimAsync(int limit, CancellationToken cancellationToken);
    Task<bool> CompleteAsync(OutboxMessage message, CancellationToken cancellationToken);
    Task ReleaseAsync(OutboxMessage message, CancellationToken cancellationToken);
}
