using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Aether.Delivery.Application;

namespace Aether.Delivery.Infrastructure.Providers;

public sealed class ResendEmailProvider(HttpClient client, IEmailAccountResolver accounts) : IEmailProvider
{
    public async Task<ProviderResult> SendAsync(EmailWorkItem item, CancellationToken cancellationToken)
    {
        var account = await accounts.ResolveAsync(item.TenantId, cancellationToken);
        if (account == null) return new(ProviderOutcome.ConfigurationError, ErrorCode: "account_not_configured");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.ApiKey);
        request.Headers.UserAgent.ParseAdd("Aether/1.0");
        request.Headers.Add("Idempotency-Key", "aether/" + item.TenantId.ToString("N") + "/" + item.DispatchId.ToString("N"));
        request.Content = JsonContent.Create(new { from = account.Sender, to = new[] { item.Recipient },
            subject = item.Subject, text = item.Text, html = item.Html,
            tags = new[] { new { name = "dispatch_id", value = item.DispatchId.ToString("D") } } });
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var delay = response.Headers.RetryAfter?.Delta
                    ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow);
                return new(ProviderOutcome.Throttled, ErrorCode: "rate_limited",
                    RetryAfter: delay is { TotalSeconds: > 0 } ? delay : TimeSpan.FromSeconds(30));
            }
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return new(ProviderOutcome.ConfigurationError, ErrorCode: "provider_authentication");
            if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout)
                return new(ProviderOutcome.Unknown, ErrorCode: "provider_uncertain");
            if (!response.IsSuccessStatusCode)
                return new(ProviderOutcome.PermanentFailure, ErrorCode: "provider_rejected");
            await response.Content.LoadIntoBufferAsync(32_768, cancellationToken);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (!document.RootElement.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String
                || !Guid.TryParse(id.GetString(), out var externalId))
                return new(ProviderOutcome.Unknown, ErrorCode: "missing_confirmation_id");
            return new(ProviderOutcome.Accepted, externalId.ToString("D"));
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or JsonException)
        {
            return new(ProviderOutcome.Unknown, ErrorCode: "transport_uncertain");
        }
    }
}
