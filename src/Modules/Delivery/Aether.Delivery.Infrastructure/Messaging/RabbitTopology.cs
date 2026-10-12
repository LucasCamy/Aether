using RabbitMQ.Client;

namespace Aether.Delivery.Infrastructure.Messaging;

public static class RabbitTopology
{
    public static async Task DeclareAsync(IChannel channel, CancellationToken cancellationToken)
    {
        foreach (var (name, type) in new[] { ("comm.dispatch.v1", ExchangeType.Direct),
            ("comm.events.v1", ExchangeType.Topic), ("comm.retry.v1", ExchangeType.Direct), ("comm.dead.v1", ExchangeType.Direct) })
            await channel.ExchangeDeclareAsync(name, type, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        foreach (var priority in new[] { "critical", "transactional", "operational", "marketing" })
        {
            var route = "email." + priority + ".shared-00";
            var queue = "comm.q." + route + ".v1";
            await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
                arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum",
                    ["x-dead-letter-exchange"] = "comm.dead.v1", ["x-dead-letter-routing-key"] = route },
                cancellationToken: cancellationToken);
            await channel.QueueBindAsync(queue, "comm.dispatch.v1", route, cancellationToken: cancellationToken);
            await channel.QueueDeclareAsync(queue + ".dead", durable: true, exclusive: false, autoDelete: false,
                arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum" }, cancellationToken: cancellationToken);
            await channel.QueueBindAsync(queue + ".dead", "comm.dead.v1", route, cancellationToken: cancellationToken);
            foreach (var seconds in new[] { 5, 30, 120, 600, 1800 })
            {
                var retryRoute = route + ".retry-" + seconds;
                var retryQueue = queue + ".retry-" + seconds;
                await channel.QueueDeclareAsync(retryQueue, durable: true, exclusive: false, autoDelete: false,
                    arguments: new Dictionary<string, object?> {
                        ["x-queue-type"] = "quorum", ["x-message-ttl"] = seconds * 1000,
                        ["x-dead-letter-exchange"] = "comm.dispatch.v1", ["x-dead-letter-routing-key"] = route },
                    cancellationToken: cancellationToken);
                await channel.QueueBindAsync(retryQueue, "comm.retry.v1", retryRoute, cancellationToken: cancellationToken);
            }
        }
    }
}
