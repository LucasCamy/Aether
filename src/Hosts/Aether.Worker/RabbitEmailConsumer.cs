using System.Text.Json;
using Aether.Contracts;
using Aether.Delivery.Application;
using Aether.Delivery.Infrastructure.Messaging;
using Aether.Notifications.Application;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Aether.Worker;

public sealed class RabbitEmailConsumer(RabbitSettings settings, IExecutionStore store,
    IEmailProvider provider, ILogger<RabbitEmailConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = new ConnectionFactory { HostName = settings.Host, Port = settings.Port,
                    UserName = settings.Username, Password = settings.Password, VirtualHost = settings.VirtualHost,
                    AutomaticRecoveryEnabled = false,
                    Ssl = new SslOption { Enabled = settings.UseTls, ServerName = settings.Host } };
                await using var connection = await factory.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await RabbitTopology.DeclareAsync(channel, stoppingToken);
                await channel.BasicQosAsync(0, 1, global: false, stoppingToken);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, delivery) =>
                {
                    try
                    {
                        var envelope = JsonSerializer.Deserialize<DispatchEnvelope>(delivery.Body.Span,
                            new JsonSerializerOptions(JsonSerializerDefaults.Web));
                        if (envelope is not { SchemaVersion: 1 } || envelope.TenantId == Guid.Empty ||
                            envelope.DispatchId == Guid.Empty || envelope.MessageId == Guid.Empty || envelope.Generation < 0)
                        {
                            await channel.BasicNackAsync(delivery.DeliveryTag, false, false, stoppingToken);
                            return;
                        }
                        var ticket = await store.BeginAsync(envelope.TenantId, envelope.DispatchId,
                            envelope.MessageId, envelope.Generation, stoppingToken);
                        if (ticket.Decision == ExecutionDecision.Busy)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                            await channel.BasicNackAsync(delivery.DeliveryTag, false, true, stoppingToken);
                            return;
                        }
                        if (ticket.Decision == ExecutionDecision.Ready)
                        {
                            using var content = JsonDocument.Parse(ticket.Content!);
                            var root = content.RootElement;
                            var item = new EmailWorkItem(ticket.TenantId, ticket.DispatchId, ticket.Recipient!,
                                root.GetProperty("subject").GetString()!, root.GetProperty("text").GetString(),
                                root.GetProperty("html").GetString());
                            ProviderResult result;
                            try { result = await provider.SendAsync(item, stoppingToken); }
                            catch (Exception) { result = new(ProviderOutcome.Unknown, ErrorCode: "unhandled_provider_error"); }
                            var kind = result.Outcome switch {
                                ProviderOutcome.Accepted => "accepted", ProviderOutcome.Throttled => "throttled",
                                ProviderOutcome.PermanentFailure => "permanent_failure", _ => "unknown"
                            };
                            await store.FinishAsync(ticket, new(kind, result.MessageId, result.ErrorCode, result.RetryAfter), stoppingToken);
                        }
                        await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
                    }
                    catch (JsonException)
                    {
                        await channel.BasicNackAsync(delivery.DeliveryTag, false, false, stoppingToken);
                    }
                    catch (Exception) when (!stoppingToken.IsCancellationRequested)
                    {
                        logger.LogWarning("Consumo sem decisão durável; mensagem será reentregue.");
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                        await channel.BasicNackAsync(delivery.DeliveryTag, false, true, stoppingToken);
                    }
                };
                foreach (var priority in new[] { "critical", "transactional", "operational", "marketing" })
                    await channel.BasicConsumeAsync("comm.q.email." + priority + ".shared-00.v1",
                        autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
                while (connection.IsOpen && !stoppingToken.IsCancellationRequested)
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Consumer indisponível; retomada após backoff.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
