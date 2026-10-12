using Aether.Notifications.Application;
using Aether.Notifications.Domain;
using Aether.Notifications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aether.Tests;

public sealed class ExecutionTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private sealed class Factory(string connection) : IDbContextFactory<NotificationDbContext>
    {
        public NotificationDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<NotificationDbContext>().UseNpgsql(connection).Options);
    }
    private async Task<Acceptance> CreateAsync()
    {
        var store = new PostgresNotificationStore(new Factory(fixture.RuntimeConnection), TimeProvider.System);
        var draft = new NotificationDraft("synthetic@example.com", "Subject", "Body", null,
            "transactional", DateTimeOffset.UtcNow.AddMinutes(10), null, "{}");
        return await new SubmitNotification(store).ExecuteAsync(fixture.TenantA, Guid.NewGuid(),
            Guid.NewGuid().ToString("D"), draft, CancellationToken.None);
    }
    private PostgresExecutionStore Store() => new(new Factory(fixture.RuntimeConnection), TimeProvider.System);

    [Fact]
    public async Task ConcurrentExecutionReservesOneAttemptAndReplayCannotResend()
    {
        var accepted = await CreateAsync();
        var store = Store();
        var id = Guid.NewGuid();
        var tickets = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            store.BeginAsync(fixture.TenantA, accepted.DispatchId, id, 0, CancellationToken.None)));
        Assert.Single(tickets, t => t.Decision == ExecutionDecision.Ready);
        Assert.Equal(4, tickets.Count(t => t.Decision == ExecutionDecision.Busy));
        var ready = tickets.Single(t => t.Decision == ExecutionDecision.Ready);
        await store.FinishAsync(ready, new ExecutionOutcome("accepted", "synthetic-id"), CancellationToken.None);
        Assert.Equal(ExecutionDecision.Skip, (await store.BeginAsync(
            fixture.TenantA, accepted.DispatchId, id, 0, CancellationToken.None)).Decision);
        await using var db = fixture.AdminDb();
        Assert.Equal(1, await db.Attempts.CountAsync(x => x.DispatchId == accepted.DispatchId));
    }

    [Fact]
    public async Task RetryDecisionAndNewOutboxAreAtomic()
    {
        var accepted = await CreateAsync();
        var store = Store();
        var ticket = await store.BeginAsync(fixture.TenantA, accepted.DispatchId, Guid.NewGuid(), 0, CancellationToken.None);
        await store.FinishAsync(ticket, new ExecutionOutcome("throttled", RetryAfter: TimeSpan.FromSeconds(5)), CancellationToken.None);
        await using var db = fixture.AdminDb();
        var dispatch = await db.Dispatches.SingleAsync(x => x.Id == accepted.DispatchId);
        Assert.Equal(ExecutionState.RetryScheduled, dispatch.State);
        Assert.Equal(1, dispatch.Generation);
        var retry = await db.Outbox.SingleAsync(x => x.DispatchId == accepted.DispatchId && x.Exchange == "comm.retry.v1");
        Assert.EndsWith(".retry-5", retry.RoutingKey);
        Assert.Equal(ExecutionDecision.Skip, (await store.BeginAsync(
            fixture.TenantA, accepted.DispatchId, Guid.NewGuid(), 0, CancellationToken.None)).Decision);
    }

    [Fact]
    public async Task CrashAfterBeginningAttemptBecomesUnknown()
    {
        var accepted = await CreateAsync();
        var store = Store();
        var id = Guid.NewGuid();
        await store.BeginAsync(fixture.TenantA, accepted.DispatchId, id, 0, CancellationToken.None);
        await using var db = fixture.AdminDb();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE notifications.dispatches SET lease_until = now() - interval '1 second' WHERE tenant_id = {fixture.TenantA} AND id = {accepted.DispatchId}");
        Assert.Equal(ExecutionDecision.Skip, (await store.BeginAsync(
            fixture.TenantA, accepted.DispatchId, id, 0, CancellationToken.None)).Decision);
        Assert.Equal(ExecutionState.OutcomeUnknown, (await db.Dispatches.AsNoTracking()
            .SingleAsync(x => x.Id == accepted.DispatchId)).State);
        Assert.Equal("unknown", (await db.Attempts.SingleAsync(x => x.DispatchId == accepted.DispatchId)).Outcome);
    }
}
