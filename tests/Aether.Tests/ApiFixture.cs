using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Aether.Notifications.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Aether.Tests;

public sealed class ApiFixture : WebApplicationFactory<Aether.Api.ApiAssemblyMarker>, IAsyncLifetime
{
    private readonly RSA rsa = RSA.Create(2048);
    public string RuntimeConnection { get; } = Environment.GetEnvironmentVariable("AETHER_TEST_RUNTIME")
        ?? throw new InvalidOperationException("Execute scripts/Test.ps1.");
    public string AdminConnection { get; } = Environment.GetEnvironmentVariable("AETHER_TEST_ADMIN")
        ?? throw new InvalidOperationException("Execute scripts/Test.ps1.");
    public Guid TenantA { get; } = Guid.NewGuid();
    public Guid TenantB { get; } = Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Notifications", RuntimeConnection);
        builder.UseSetting("Auth:Issuer", "https://aether.test");
        builder.UseSetting("Auth:Audience", "aether-tests");
        builder.UseSetting("Auth:PublicKeyPem", rsa.ExportSubjectPublicKeyInfoPem());
    }

    public async Task InitializeAsync()
    {
        await using var db = AdminDb();
        await db.Database.MigrateAsync();
        db.Tenants.AddRange(new TenantRecord { Id = TenantA }, new TenantRecord { Id = TenantB });
        await db.SaveChangesAsync();
    }
    Task IAsyncLifetime.DisposeAsync() { rsa.Dispose(); Dispose(); return Task.CompletedTask; }
    public NotificationDbContext AdminDb() => new(
        new DbContextOptionsBuilder<NotificationDbContext>().UseNpgsql(AdminConnection).Options);
    public string Token(Guid tenant, params string[] permissions)
    {
        var claims = new List<Claim> { new("sub", "integration-test"), new("tenant_id", tenant.ToString("D")) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("https://aether.test",
            "aether-tests", claims, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256)));
    }
}
