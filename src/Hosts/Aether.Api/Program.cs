using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Aether.Api;
using Aether.Notifications.Application;
using Aether.Notifications.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
var connection = builder.Configuration.GetConnectionString("Notifications")
    ?? throw new InvalidOperationException("ConnectionStrings:Notifications é obrigatório.");
builder.Services.AddDbContextFactory<NotificationDbContext>(options => options.UseNpgsql(connection));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<INotificationStore, PostgresNotificationStore>();
builder.Services.AddScoped<SubmitNotification>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1_048_576);

if (args.Contains("--migrate", StringComparer.Ordinal))
{
    await using var migrationHost = builder.Build();
    await using var db = await migrationHost.Services.GetRequiredService<IDbContextFactory<NotificationDbContext>>().CreateDbContextAsync();
    await db.Database.MigrateAsync();
    return;
}

var issuer = builder.Configuration["Auth:Issuer"]
    ?? throw new InvalidOperationException("Auth:Issuer é obrigatório.");
var audience = builder.Configuration["Auth:Audience"]
    ?? throw new InvalidOperationException("Auth:Audience é obrigatório.");
var authority = builder.Configuration["Auth:Authority"];
var publicKey = builder.Configuration["Auth:PublicKeyPem"];
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.IncludeErrorDetails = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = issuer,
        ValidateAudience = true, ValidAudience = audience,
        ValidateLifetime = true, RequireExpirationTime = true,
        RequireSignedTokens = true, ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.FromSeconds(30)
    };
    if ((builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
        && !string.IsNullOrWhiteSpace(publicKey))
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(publicKey);
        options.TokenValidationParameters.IssuerSigningKey = new RsaSecurityKey(rsa.ExportParameters(false));
        options.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.RsaSha256];
    }
    else
    {
        if (!Uri.TryCreate(authority, UriKind.Absolute, out var uri) || uri.Scheme != "https")
            throw new InvalidOperationException("Auth:Authority HTTPS é obrigatório fora do modo local.");
        options.Authority = authority;
        options.RequireHttpsMetadata = true;
    }
});
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantMiddleware>();
app.MapGet("/health/live", () => Results.Ok(new { status = "alive", service = "Aether.Api" }));
app.MapGet("/health/ready", async (IDbContextFactory<NotificationDbContext> factory, CancellationToken cancellationToken) =>
{
    try
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await PostgresNotificationStore.SetTenant(db, Guid.Empty, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SELECT 1 FROM tenancy.tenants LIMIT 0", cancellationToken);
        return Results.Ok(new { status = "ready" });
    }
    catch (Npgsql.NpgsqlException) { return Results.StatusCode(503); }
    catch (InvalidOperationException) { return Results.StatusCode(503); }
});
app.MapNotifications();
app.Run();

public partial class Program;
