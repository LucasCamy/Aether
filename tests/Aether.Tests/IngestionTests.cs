using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Aether.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.AspNetCore.Hosting;

namespace Aether.Tests;

public sealed class IngestionTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task ReadinessRequiresActualCatalogSchema()
    {
        using var client = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }
    private static object Payload(string priority = "transactional", string text = "Pedido confirmado.") => new
    {
        priority, deliveryMode = "fan_out", recipient = new { email = "synthetic@example.com" },
        messages = new[] { new { channel = "email", content = new { subject = "Confirmação", text } } },
        expiresAt = DateTimeOffset.UtcNow.AddHours(1), metadata = new { source = "test" }
    };

    [Fact]
    public async Task DatabaseUnavailableNeverReturnsAccepted()
    {
        var badConnection = new NpgsqlConnectionStringBuilder(fixture.RuntimeConnection) { Port = 1, Timeout = 1 };
        await using var application = fixture.WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Notifications", badConnection.ConnectionString));
        using var client = application.CreateClient();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.SendAsync(Request(HttpMethod.Post,
            "/v1/notifications", fixture.TenantA, Payload(), Guid.NewGuid().ToString("D")))).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task CompositeForeignKeyRejectsCrossTenantAssociation()
    {
        using var client = fixture.CreateClient();
        var response = await client.SendAsync(Request(HttpMethod.Post, "/v1/notifications",
            fixture.TenantA, Payload(), Guid.NewGuid().ToString("D")));
        var accepted = (await response.Content.ReadFromJsonAsync<NotificationAcceptedResponse>())!;
        await using var connection = new NpgsqlConnection(fixture.RuntimeConnection);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var set = new NpgsqlCommand("SELECT set_config('aether.tenant_id', $1, true)", connection, transaction);
        set.Parameters.AddWithValue(fixture.TenantB.ToString("D"));
        await set.ExecuteNonQueryAsync();
        await using var insert = new NpgsqlCommand("""
            INSERT INTO notifications.dispatch_registry(tenant_id,id,request_id,created_month) VALUES ($1,$2,$3,$4)
            """, connection, transaction);
        insert.Parameters.AddWithValue(fixture.TenantB);
        insert.Parameters.AddWithValue(Guid.NewGuid());
        insert.Parameters.AddWithValue(accepted.NotificationId);
        insert.Parameters.AddWithValue(new DateOnly(2026, 10, 1));
        Assert.Equal("23503", (await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync())).SqlState);
    }
    private HttpRequestMessage Request(HttpMethod method, string path, Guid tenant, object? body = null, string? key = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
            fixture.Token(tenant, "notifications:send", "notifications:read"));
        request.Headers.Add("X-Tenant-ID", tenant.ToString("D"));
        request.Headers.Add("X-Correlation-ID", Guid.NewGuid().ToString("D"));
        if (key != null) request.Headers.Add("X-Idempotency-Key", key);
        if (body != null) request.Content = JsonContent.Create(body);
        return request;
    }
    [Fact]
    public async Task ConcurrentReplayCreatesOneRequestDispatchOutbox()
    {
        using var client = fixture.CreateClient();
        var payload = Payload();
        var key = Guid.NewGuid().ToString("D");
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            client.SendAsync(Request(HttpMethod.Post, "/v1/notifications", fixture.TenantA, payload, key))));
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Accepted, r.StatusCode));
        var accepted = await Task.WhenAll(responses.Select(r => r.Content.ReadFromJsonAsync<NotificationAcceptedResponse>()));
        var id = accepted[0]!.NotificationId;
        Assert.All(accepted, value => Assert.Equal(id, value!.NotificationId));
        await using var db = fixture.AdminDb();
        Assert.Equal(1, await db.Requests.CountAsync(x => x.Id == id));
        var dispatch = await db.Dispatches.SingleAsync(x => x.RequestId == id);
        Assert.Equal(1, await db.Outbox.CountAsync(x => x.DispatchId == dispatch.Id));
        Assert.Null((await db.Outbox.SingleAsync(x => x.DispatchId == dispatch.Id)).PublishedAt);
    }
    [Fact]
    public async Task SameKeyDifferentPayloadConflicts()
    {
        using var client = fixture.CreateClient();
        var key = Guid.NewGuid().ToString("D");
        Assert.Equal(HttpStatusCode.Accepted, (await client.SendAsync(
            Request(HttpMethod.Post, "/v1/notifications", fixture.TenantA, Payload(), key))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(
            Request(HttpMethod.Post, "/v1/notifications", fixture.TenantA, Payload(text: "Outro"), key))).StatusCode);
    }
    [Fact]
    public async Task ForeignTenantCannotReadOrSpoof()
    {
        using var client = fixture.CreateClient();
        var response = await client.SendAsync(Request(HttpMethod.Post, "/v1/notifications",
            fixture.TenantA, Payload(), Guid.NewGuid().ToString("D")));
        var accepted = await response.Content.ReadFromJsonAsync<NotificationAcceptedResponse>();
        var path = "/v1/notifications/" + accepted!.NotificationId;
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(Request(HttpMethod.Get, path, fixture.TenantB))).StatusCode);
        var spoof = Request(HttpMethod.Get, path, fixture.TenantB);
        spoof.Headers.Remove("X-Tenant-ID");
        spoof.Headers.Add("X-Tenant-ID", fixture.TenantA.ToString("D"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(spoof)).StatusCode);
    }
    [Fact]
    public async Task MissingOrForgedJwtRejected()
    {
        using var client = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/v1/notifications/" + Guid.NewGuid())).StatusCode);
        var forged = Request(HttpMethod.Get, "/v1/notifications/" + Guid.NewGuid(), fixture.TenantA);
        var token = forged.Headers.Authorization!.Parameter!;
        forged.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token[..^10] + "AAAAAAAAAA");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(forged)).StatusCode);
    }
    [Fact]
    public async Task CriticalRequiresPermission()
    {
        using var client = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Request(HttpMethod.Post,
            "/v1/notifications", fixture.TenantA, Payload(priority: "critical"), Guid.NewGuid().ToString("D")))).StatusCode);
    }
    [Fact]
    public async Task RlsAndPoolingDoNotLeak()
    {
        using var client = fixture.CreateClient();
        await client.SendAsync(Request(HttpMethod.Post, "/v1/notifications", fixture.TenantA,
            Payload(), Guid.NewGuid().ToString("D")));
        var settings = new NpgsqlConnectionStringBuilder(fixture.RuntimeConnection) { MaxPoolSize = 1 };
        await using var source = NpgsqlDataSource.Create(settings.ConnectionString);
        await using (var connection = await source.OpenConnectionAsync())
        {
            await using var transaction = await connection.BeginTransactionAsync();
            await using var set = new NpgsqlCommand("SELECT set_config('aether.tenant_id', $1, true)", connection, transaction);
            set.Parameters.AddWithValue(fixture.TenantA.ToString("D"));
            await set.ExecuteNonQueryAsync();
            await using var query = new NpgsqlCommand("SELECT count(*) FROM notifications.notification_requests", connection, transaction);
            Assert.True(Convert.ToInt32(await query.ExecuteScalarAsync()) > 0);
            await transaction.CommitAsync();
        }
        await using (var connection = await source.OpenConnectionAsync())
        {
            await using var query = new NpgsqlCommand("SELECT count(*) FROM notifications.notification_requests", connection);
            Assert.Equal(0, Convert.ToInt32(await query.ExecuteScalarAsync()));
            await using var role = new NpgsqlCommand("SELECT rolsuper OR rolbypassrls FROM pg_roles WHERE rolname = current_user", connection);
            Assert.False((bool)(await role.ExecuteScalarAsync())!);
        }
    }
    [Fact]
    public async Task OutboxFailureRollsBackAcceptance()
    {
        await using var db = fixture.AdminDb();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE OR REPLACE FUNCTION notifications.test_reject_outbox() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW.routing_key = 'email.operational.shared-00' THEN RAISE EXCEPTION 'test rejection'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER test_reject_outbox BEFORE INSERT ON notifications.outbox_messages
            FOR EACH ROW EXECUTE FUNCTION notifications.test_reject_outbox();
            """);
        var key = Guid.NewGuid().ToString("D");
        var before = await db.Requests.CountAsync(x => x.TenantId == fixture.TenantA);
        try
        {
            using var client = fixture.CreateClient();
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.SendAsync(Request(HttpMethod.Post,
                "/v1/notifications", fixture.TenantA, Payload(priority: "operational"), key))).StatusCode);
            Assert.False(await db.Idempotency.AnyAsync(x => x.Key == key));
            Assert.Equal(before, await db.Requests.CountAsync(x => x.TenantId == fixture.TenantA));
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER test_reject_outbox ON notifications.outbox_messages; DROP FUNCTION notifications.test_reject_outbox();");
        }
    }
}
