using System.Text.Json;
using Aether.Notifications.Application;
using Aether.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aether.Notifications.Infrastructure.Persistence;

public sealed class PostgresExecutionStore(IDbContextFactory<NotificationDbContext> factory, TimeProvider clock) : IExecutionStore
{
    public async Task<ExecutionTicket> BeginAsync(Guid tenantId, Guid dispatchId, Guid messageId,
        int generation, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await PostgresNotificationStore.SetTenant(db, tenantId, cancellationToken);
        var dispatch = await db.Dispatches.FromSqlInterpolated(
            $"SELECT * FROM notifications.dispatches WHERE tenant_id = {tenantId} AND id = {dispatchId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var inbox = await db.Inbox.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.MessageId == messageId, cancellationToken);
        if (dispatch == null || inbox?.CompletedAt != null || dispatch.Generation != generation)
            return new(ExecutionDecision.Skip, tenantId, dispatchId, messageId);
        if (dispatch.State == ExecutionState.InFlight)
        {
            if (dispatch.LeaseUntil > now) return new(ExecutionDecision.Busy, tenantId, dispatchId, messageId);
            dispatch.RecoverExpiredLease(now);
            var attempt = await db.Attempts.SingleOrDefaultAsync(
                x => x.TenantId == tenantId && x.DispatchId == dispatchId && x.Number == dispatch.AttemptCount, cancellationToken);
            if (attempt != null) { attempt.Outcome = "unknown"; attempt.FinishedAt = now; }
        }
        if (inbox == null)
        {
            inbox = new InboxRecord { TenantId = tenantId, MessageId = messageId, CreatedAt = now };
            db.Inbox.Add(inbox);
        }
        if (now >= dispatch.ExpiresAt) dispatch.Expire(now);
        if (dispatch.State is not (ExecutionState.Pending or ExecutionState.RetryScheduled))
        {
            inbox.CompletedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(ExecutionDecision.Skip, tenantId, dispatchId, messageId);
        }
        if (dispatch.NextAttemptAt > now) return new(ExecutionDecision.Busy, tenantId, dispatchId, messageId);
        var token = dispatch.BeginAttempt(now, TimeSpan.FromSeconds(60));
        db.Attempts.Add(new AttemptRecord { TenantId = tenantId, Id = Guid.CreateVersion7(),
            DispatchId = dispatchId, CreatedMonth = dispatch.CreatedMonth,
            Number = dispatch.AttemptCount, LeaseToken = token, StartedAt = now });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(ExecutionDecision.Ready, tenantId, dispatchId, messageId, token, dispatch.Recipient, dispatch.Content);
    }

    public async Task FinishAsync(ExecutionTicket ticket, ExecutionOutcome outcome, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await PostgresNotificationStore.SetTenant(db, ticket.TenantId, cancellationToken);
        var dispatch = await db.Dispatches.FromSqlInterpolated(
            $"SELECT * FROM notifications.dispatches WHERE tenant_id = {ticket.TenantId} AND id = {ticket.DispatchId} FOR UPDATE")
            .SingleAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var attempt = await db.Attempts.SingleAsync(x => x.TenantId == ticket.TenantId
            && x.DispatchId == ticket.DispatchId && x.LeaseToken == ticket.LeaseToken, cancellationToken);
        var state = outcome.Kind switch {
            "accepted" => ExecutionState.ProviderAccepted,
            "permanent_failure" => ExecutionState.Failed,
            "throttled" when dispatch.AttemptCount < 6 => ExecutionState.RetryScheduled,
            _ => ExecutionState.OutcomeUnknown
        };
        if (outcome.Kind == "throttled" && dispatch.AttemptCount >= 6) state = ExecutionState.Failed;
        var retryAt = now + (outcome.RetryAfter ?? TimeSpan.FromSeconds(30));
        if (state == ExecutionState.RetryScheduled && retryAt >= dispatch.ExpiresAt) state = ExecutionState.Failed;
        dispatch.Finish(ticket.LeaseToken!.Value, state, now, state == ExecutionState.RetryScheduled ? retryAt : null);
        attempt.FinishedAt = now;
        attempt.Outcome = outcome.Kind;
        attempt.ProviderMessageId = outcome.ProviderMessageId;
        attempt.ErrorCode = outcome.ErrorCode;
        var inbox = await db.Inbox.SingleAsync(x => x.TenantId == ticket.TenantId && x.MessageId == ticket.MessageId, cancellationToken);
        inbox.CompletedAt = now;
        if (state == ExecutionState.RetryScheduled)
        {
            var request = await db.Requests.SingleAsync(x => x.TenantId == ticket.TenantId && x.Id == dispatch.RequestId, cancellationToken);
            var delay = Math.Max(5, (retryAt - now).TotalSeconds);
            var ttl = new[] { 5, 30, 120, 600, 1800 }.FirstOrDefault(x => x >= delay, 1800);
            var id = Guid.CreateVersion7();
            db.Outbox.Add(new OutboxRecord { TenantId = ticket.TenantId, Id = id, DispatchId = dispatch.Id,
                Exchange = "comm.retry.v1", RoutingKey = "email." + request.Priority + ".shared-00.retry-" + ttl,
                CreatedAt = now, AvailableAt = retryAt.AddSeconds(-ttl) < now ? now : retryAt.AddSeconds(-ttl),
                Payload = JsonSerializer.Serialize(new { schemaVersion = 1, messageId = id,
                    tenantId = ticket.TenantId, dispatchId = dispatch.Id, notificationId = request.Id,
                    correlationId = request.CorrelationId, generation = dispatch.Generation }) });
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
