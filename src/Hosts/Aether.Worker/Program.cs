using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Aether.Delivery.Infrastructure.Messaging;
using Aether.Notifications.Application;
using Aether.Notifications.Infrastructure.Persistence;
using Aether.Worker;
using Npgsql;
using Aether.Delivery.Application;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
if (builder.Configuration.GetValue<bool>("Worker:PublisherEnabled"))
{
    var connection = builder.Configuration.GetConnectionString("Publisher")
        ?? throw new InvalidOperationException("ConnectionStrings:Publisher é obrigatório.");
    var hostName = builder.Configuration["Rabbit:Host"] ?? "localhost";
    var local = builder.Environment.IsDevelopment();
    var tls = builder.Configuration.GetValue("Rabbit:UseTls", true);
    if (!tls && !local) throw new InvalidOperationException("RabbitMQ TLS obrigatório fora de Development.");
    builder.Services.AddSingleton(NpgsqlDataSource.Create(connection));
    builder.Services.AddSingleton<IOutboxStore, PostgresOutboxStore>();
    builder.Services.AddSingleton(new RabbitSettings(hostName,
        builder.Configuration.GetValue("Rabbit:Port", tls ? 5671 : 5672),
        builder.Configuration["Rabbit:Username"] ?? throw new InvalidOperationException("Rabbit:Username obrigatório."),
        builder.Configuration["Rabbit:Password"] ?? throw new InvalidOperationException("Rabbit:Password obrigatório."),
        builder.Configuration["Rabbit:VirtualHost"] ?? "/", tls));
    builder.Services.AddSingleton<RabbitTransport>();
    builder.Services.AddHostedService<OutboxPublisher>();
    if (builder.Configuration.GetValue<bool>("Worker:ControlledConsumerEnabled"))
    {
        if (!local) throw new InvalidOperationException("Consumer controlado permitido apenas em Development.");
        var runtime = builder.Configuration.GetConnectionString("Notifications")
            ?? throw new InvalidOperationException("ConnectionStrings:Notifications obrigatório.");
        builder.Services.AddDbContextFactory<NotificationDbContext>(options => options.UseNpgsql(runtime));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IExecutionStore, PostgresExecutionStore>();
        builder.Services.AddSingleton<IEmailProvider, ControlledEmailProvider>();
        builder.Services.AddHostedService<RabbitEmailConsumer>();
    }
}
using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Aether.Worker");
logger.LogInformation("Aether Worker iniciado; publisher habilitado: {Enabled}.",
    builder.Configuration.GetValue<bool>("Worker:PublisherEnabled"));

await host.RunAsync();
