using System.Diagnostics.Metrics;
using Aether.Delivery.Infrastructure.Messaging;
using Aether.Notifications.Application;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aether.Worker;

public sealed class OutboxPublisher(IOutboxStore store, RabbitTransport transport,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private static readonly Meter Meter = new("Aether.Outbox", "1.0.0");
    private static readonly Counter<long> Published = Meter.CreateCounter<long>("comm.outbox.published");
    private static readonly Counter<long> Failures = Meter.CreateCounter<long>("comm.outbox.failures");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var batch = await store.ClaimAsync(5, stoppingToken);
                foreach (var message in batch)
                {
                    try
                    {
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        timeout.CancelAfter(TimeSpan.FromSeconds(5));
                        await transport.PublishAsync(message.Exchange, message.RoutingKey, message.Payload, message.Id, timeout.Token);
                        if (await store.CompleteAsync(message, stoppingToken)) Published.Add(1);
                    }
                    catch (Exception) when (!stoppingToken.IsCancellationRequested)
                    {
                        Failures.Add(1);
                        logger.LogWarning("Publicação não confirmada; outbox mantida para retry.");
                        await store.ReleaseAsync(message, stoppingToken);
                    }
                }
                await Task.Delay(batch.Length == 0 ? TimeSpan.FromSeconds(1) : TimeSpan.FromMilliseconds(100), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Publisher indisponível; retomada após backoff.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
