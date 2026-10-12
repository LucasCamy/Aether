using Aether.Delivery.Infrastructure.Messaging;
using Aether.Notifications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RabbitMQ.Client;

namespace Aether.Tests;

public sealed class MessagingTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static string Password => Environment.GetEnvironmentVariable("AETHER_TEST_RABBIT_PASSWORD")
        ?? throw new InvalidOperationException("Execute scripts/Test.ps1.");
    private static RabbitSettings Settings => new("127.0.0.1", 55672, "aether", Password, UseTls: false);
    private static async Task<IConnection> Connect() => await new ConnectionFactory {
        HostName = "127.0.0.1", Port = 55672, UserName = "aether", Password = Password
    }.CreateConnectionAsync();

    [Fact]
    public async Task MandatoryUnroutablePublicationFails()
    {
        await using var transport = new RabbitTransport(Settings);
        await Assert.ThrowsAnyAsync<Exception>(() => transport.PublishAsync(
            "comm.dispatch.v1", "missing.route", "{}", Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task ConfirmedMessageSurvivesRequeueAndManualAck()
    {
        var id = Guid.NewGuid();
        var queue = "aether.test." + id.ToString("N");
        var route = "test." + id.ToString("N");
        await using var connection = await Connect();
        await using var channel = await connection.CreateChannelAsync();
        await channel.ExchangeDeclareAsync("comm.dispatch.v1", ExchangeType.Direct, true);
        await channel.QueueDeclareAsync(queue, true, false, false,
            new Dictionary<string, object?> { ["x-queue-type"] = "quorum" });
        await channel.QueueBindAsync(queue, "comm.dispatch.v1", route);
        try
        {
            await using var transport = new RabbitTransport(Settings);
            await transport.PublishAsync("comm.dispatch.v1", route, "{}", id, CancellationToken.None);
            var first = await channel.BasicGetAsync(queue, autoAck: false);
            Assert.NotNull(first);
            await channel.BasicNackAsync(first.DeliveryTag, multiple: false, requeue: true);
            var second = await channel.BasicGetAsync(queue, autoAck: false);
            Assert.NotNull(second);
            Assert.True(second.Redelivered);
            Assert.Equal(id.ToString("D"), second.BasicProperties.MessageId);
            await channel.BasicAckAsync(second.DeliveryTag, multiple: false);
            Assert.Null(await channel.BasicGetAsync(queue, autoAck: true));
        }
        finally { await channel.QueueDeleteAsync(queue); }
    }

    [Fact]
    public async Task PublisherHasNoTableAccessAndLeaseUsesFencing()
    {
        var connectionString = Environment.GetEnvironmentVariable("AETHER_TEST_PUBLISHER")!;
        await using var source = NpgsqlDataSource.Create(connectionString);
        await using (var denied = source.CreateCommand("SELECT * FROM notifications.outbox_messages"))
        {
            var exception = await Assert.ThrowsAsync<PostgresException>(() => denied.ExecuteNonQueryAsync());
            Assert.Equal("42501", exception.SqlState);
        }
        var store = new PostgresOutboxStore(source);
        var first = await store.ClaimAsync(5, CancellationToken.None);
        Assert.NotEmpty(first);
        var second = await store.ClaimAsync(5, CancellationToken.None);
        Assert.Empty(first.Select(x => x.Id).Intersect(second.Select(x => x.Id)));
        var message = first[0];
        await using var db = fixture.AdminDb();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE notifications.outbox_messages SET lease_until = now() - interval '1 second' WHERE tenant_id = {message.TenantId} AND id = {message.Id}");
        Assert.False(await store.CompleteAsync(message, CancellationToken.None));
        foreach (var item in first.Concat(second)) await store.ReleaseAsync(item, CancellationToken.None);
    }
}
