using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aether.Notifications.Domain;

namespace Aether.Notifications.Application;

public sealed record NotificationDraft(string Email, string Subject, string? Text,
    string? Html, string Priority, DateTimeOffset ExpiresAt, string? ClientReference, string Metadata);
public sealed record Acceptance(Guid NotificationId, DateTimeOffset AcceptedAt,
    DateTimeOffset IdempotencyExpiresAt, Guid DispatchId, bool Replayed);
public sealed record NotificationView(NotificationRequest Request, Dispatch[] Dispatches);

public sealed class NotificationFault(string code, int status) : Exception(code)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

public interface INotificationStore
{
    Task<Acceptance> SubmitAsync(Guid tenantId, Guid correlationId, string key,
        NotificationDraft draft, byte[] hash, CancellationToken cancellationToken);
    Task<NotificationView?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
}

public sealed class SubmitNotification(INotificationStore store)
{
    public Task<Acceptance> ExecuteAsync(Guid tenantId, Guid correlationId, string key,
        NotificationDraft draft, CancellationToken cancellationToken)
    {
        if (key.Length is < 16 or > 128 || key.Any(c => c < 33 || c > 126))
            throw new NotificationFault("invalid_idempotency_key", 400);
        if (!MailAddress.TryCreate(draft.Email, out var address) || address.Address != draft.Email
            || draft.Email.Length > 254)
            throw new NotificationFault("invalid_recipient", 422);
        if (string.IsNullOrWhiteSpace(draft.Subject) || draft.Subject.Length > 998
            || draft.Subject.Any(c => c is '\r' or '\n') ||
            (string.IsNullOrWhiteSpace(draft.Text) && string.IsNullOrWhiteSpace(draft.Html)))
            throw new NotificationFault("invalid_content", 422);
        if (draft.Priority is not ("transactional" or "operational" or "marketing" or "critical"))
            throw new NotificationFault("invalid_priority", 422);
        if (draft.ClientReference?.Length > 200 || Encoding.UTF8.GetByteCount(draft.Metadata) > 8192
            || (draft.Text?.Length ?? 0) + (draft.Html?.Length ?? 0) > 500_000)
            throw new NotificationFault("payload_limit_exceeded", 422);
        using var metadata = JsonDocument.Parse(draft.Metadata);
        if (metadata.RootElement.ValueKind != JsonValueKind.Object)
            throw new NotificationFault("invalid_metadata", 422);
        var json = JsonSerializer.Serialize(new {
            operation = "POST:/v1/notifications:v1", draft.Email, draft.Subject,
            draft.Text, draft.Html, draft.Priority,
            expiresAt = draft.ExpiresAt.ToUniversalTime(), draft.ClientReference,
            metadata = metadata.RootElement
        });
        return store.SubmitAsync(tenantId, correlationId, key, draft,
            SHA256.HashData(CanonicalJson.Encode(json)), cancellationToken);
    }
}

public static class CanonicalJson
{
    public static byte[] Encode(string json)
    {
        using var document = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, document.RootElement);
        return stream.ToArray();
    }
    private static void Write(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            var properties = element.EnumerateObject().ToArray();
            if (properties.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length)
                throw new NotificationFault("duplicate_json_property", 400);
            foreach (var property in properties.OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                Write(writer, property.Value);
            }
            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in element.EnumerateArray()) Write(writer, item);
            writer.WriteEndArray();
        }
        else if (element.ValueKind == JsonValueKind.Number)
        {
            var raw = element.GetRawText();
            var exponentIndex = raw.IndexOfAny(['e', 'E']);
            var mantissa = exponentIndex < 0 ? raw : raw[..exponentIndex];
            var exponentText = exponentIndex < 0 ? "0" : raw[(exponentIndex + 1)..];
            if (!int.TryParse(exponentText, System.Globalization.NumberStyles.AllowLeadingSign,
                System.Globalization.CultureInfo.InvariantCulture, out var exponent) || Math.Abs((long)exponent) > 100_000)
                throw new NotificationFault("numeric_range_exceeded", 422);
            var negative = mantissa.StartsWith('-');
            if (negative) mantissa = mantissa[1..];
            var decimalIndex = mantissa.IndexOf('.');
            var fractionLength = decimalIndex < 0 ? 0 : mantissa.Length - decimalIndex - 1;
            var digits = mantissa.Replace(".", "", StringComparison.Ordinal).TrimStart('0');
            if (digits.Length == 0) { writer.WriteNumberValue(0); return; }
            var coefficient = digits.TrimEnd('0');
            var scale = (long)exponent - fractionLength + digits.Length - coefficient.Length;
            writer.WriteRawValue((negative ? "-" : "") + coefficient + "e" +
                scale.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        else element.WriteTo(writer);
    }
}
