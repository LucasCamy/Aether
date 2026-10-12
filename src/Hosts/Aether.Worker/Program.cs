using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Aether.Worker");
logger.LogInformation("Aether Worker bootstrap iniciado. Consumers e jobs ainda não estão registrados.");

await host.RunAsync();
