using Aether.Delivery.Infrastructure.Messaging;
using Aether.Notifications.Application;
using Aether.Notifications.Domain;
using Aether.Notifications.Infrastructure.Persistence;
using Aether.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Aether.Tests;

public sealed class PipelineTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private sealed class Factory(string connection) : IDbContextFactory<NotificationDbContext>
    {
        public NotificationDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<NotificationDbContext>().UseNpgsql(connection).Options);
    }

    [Fact]
    public async Task DurableAcceptanceFlowsThroughBrokerToControlledConsumer()
    {
        var factory = new Factory(fixture.RuntimeConnection);
        var store = new PostgresNotificationStore(factory, TimeProvider.System);
        var accepted = await new SubmitNotification(store).ExecuteAsync(fixture.TenantA, Guid.NewGuid(),
            Guid.NewGuid().ToString("D"), new NotificationDraft("synthetic@example.com", "Subject", "Body", null,
                "transactional", DateTimeOffset.UtcNow.AddMinutes(10), null, "{}"), CancellationToken.None);
        var settings = new RabbitSettings("127.0.0.1", 55672, "aether",
            Environment.GetEnvironmentVariable("AETHER_TEST_RABBIT_PASSWORD")!, UseTls: false);
        await using var source = NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("AETHER_TEST_PUBLISHER")!);
        await using var transport = new RabbitTransport(settings);
        using var publisher = new OutboxPublisher(new PostgresOutboxStore(source), transport, NullLogger<OutboxPublisher>.Instance);
        using var consumer = new RabbitEmailConsumer(settings, new PostgresExecutionStore(factory, TimeProvider.System),
            new ControlledEmailProvider(), NullLogger<RabbitEmailConsumer>.Instance);
        await consumer.StartAsync(CancellationToken.None);
        await publisher.StartAsync(CancellationToken.None);
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (true)
            {
                await using var db = fixture.AdminDb();
                var state = await db.Dispatches.AsNoTracking().Where(x => x.Id == accepted.DispatchId)
                    .Select(x => x.State).SingleAsync(timeout.Token);
                if (state == ExecutionState.ProviderAccepted)
                {
                    if ((await db.Outbox.SingleAsync(x => x.DispatchId == accepted.DispatchId, timeout.Token)).PublishedAt == null)
                    {
                        await Task.Delay(100, timeout.Token);
                        continue;
                    }
                    Assert.Equal(1, await db.Attempts.CountAsync(x => x.DispatchId == accepted.DispatchId, timeout.Token));
                    Assert.NotNull((await db.Outbox.SingleAsync(x => x.DispatchId == accepted.DispatchId, timeout.Token)).PublishedAt);
                    break;
                }
                await Task.Delay(100, timeout.Token);
            }
        }
        finally
        {
            await publisher.StopAsync(CancellationToken.None);
            await consumer.StopAsync(CancellationToken.None);
        }
    }
}
