namespace Aether.Notifications.Application;

public enum ExecutionDecision { Ready, Busy, Skip }
public sealed record ExecutionTicket(ExecutionDecision Decision, Guid TenantId, Guid DispatchId,
    Guid MessageId, Guid? LeaseToken = null, string? Recipient = null, string? Content = null);
public sealed record ExecutionOutcome(string Kind, string? ProviderMessageId = null,
    string? ErrorCode = null, TimeSpan? RetryAfter = null);
public interface IExecutionStore
{
    Task<ExecutionTicket> BeginAsync(Guid tenantId, Guid dispatchId, Guid messageId, int generation,
        CancellationToken cancellationToken);
    Task FinishAsync(ExecutionTicket ticket, ExecutionOutcome outcome, CancellationToken cancellationToken);
}
