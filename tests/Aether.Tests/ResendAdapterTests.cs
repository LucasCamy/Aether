using System.Net;
using System.Net.Http.Json;
using Aether.Delivery.Application;
using Aether.Delivery.Infrastructure.Providers;

namespace Aether.Tests;

public sealed class ResendAdapterTests
{
    private sealed class Accounts : IEmailAccountResolver
    {
        public Task<EmailAccount?> ResolveAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<EmailAccount?>(new EmailAccount("synthetic-test-key", "sender@example.com"));
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response(request));
    }
    private static EmailWorkItem Item() => new(Guid.NewGuid(), Guid.NewGuid(), "synthetic@example.com", "Subject", "Body", null);

    [Fact]
    public async Task AdapterUsesStableIdempotencyKeyAndFixedHttpsEndpoint()
    {
        var item = Item();
        var id = Guid.NewGuid();
        var keys = new List<string>();
        using var client = new HttpClient(new Handler(request => {
            Assert.Equal("https://api.resend.com/emails", request.RequestUri!.ToString());
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.NotEmpty(request.Headers.UserAgent);
            keys.Add(request.Headers.GetValues("Idempotency-Key").Single());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { id }) };
        }));
        var provider = new ResendEmailProvider(client, new Accounts());
        Assert.Equal(ProviderOutcome.Accepted, (await provider.SendAsync(item, CancellationToken.None)).Outcome);
        await provider.SendAsync(item, CancellationToken.None);
        Assert.Equal(keys[0], keys[1]);
    }

    [Theory]
    [InlineData(429, ProviderOutcome.Throttled)]
    [InlineData(500, ProviderOutcome.Unknown)]
    [InlineData(408, ProviderOutcome.Unknown)]
    [InlineData(401, ProviderOutcome.ConfigurationError)]
    [InlineData(422, ProviderOutcome.PermanentFailure)]
    public async Task ClassifiesResponsesWithoutBlindRetry(int code, ProviderOutcome expected)
    {
        using var client = new HttpClient(new Handler(_ => new HttpResponseMessage((HttpStatusCode)code)));
        Assert.Equal(expected, (await new ResendEmailProvider(client, new Accounts())
            .SendAsync(Item(), CancellationToken.None)).Outcome);
    }

    [Fact]
    public async Task TimeoutIsUnknown()
    {
        using var client = new HttpClient(new Handler(_ => throw new TaskCanceledException()));
        Assert.Equal(ProviderOutcome.Unknown, (await new ResendEmailProvider(client, new Accounts())
            .SendAsync(Item(), CancellationToken.None)).Outcome);
    }
}
