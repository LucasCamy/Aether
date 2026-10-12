using System.Text;
using RabbitMQ.Client;

namespace Aether.Delivery.Infrastructure.Messaging;

public sealed record RabbitSettings(string Host, int Port, string Username, string Password,
    string VirtualHost = "/", bool UseTls = true);

public sealed class RabbitTransport(RabbitSettings settings) : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private IConnection? connection;
    private IChannel? channel;

    public async Task PublishAsync(string exchange, string route, string payload, Guid id, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureConnected(cancellationToken);
            var properties = new BasicProperties { Persistent = true, ContentType = "application/json", MessageId = id.ToString("D") };
            await channel!.BasicPublishAsync(exchange, route, mandatory: true, properties,
                Encoding.UTF8.GetBytes(payload), cancellationToken);
        }
        catch
        {
            if (channel != null) await channel.DisposeAsync();
            if (connection != null) await connection.DisposeAsync();
            channel = null;
            connection = null;
            throw;
        }
        finally { gate.Release(); }
    }

    private async Task EnsureConnected(CancellationToken cancellationToken)
    {
        if (connection?.IsOpen == true && channel?.IsOpen == true) return;
        if (connection != null) await connection.DisposeAsync();
        var factory = new ConnectionFactory { HostName = settings.Host, Port = settings.Port,
            UserName = settings.Username, Password = settings.Password, VirtualHost = settings.VirtualHost,
            AutomaticRecoveryEnabled = false, RequestedConnectionTimeout = TimeSpan.FromSeconds(5),
            Ssl = new SslOption { Enabled = settings.UseTls, ServerName = settings.Host } };
        connection = await factory.CreateConnectionAsync(cancellationToken);
        channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);
        await RabbitTopology.DeclareAsync(channel, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (channel != null) await channel.DisposeAsync();
        if (connection != null) await connection.DisposeAsync();
        gate.Dispose();
    }
}
