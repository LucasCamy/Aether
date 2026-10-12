using Aether.Contracts;
using Aether.Notifications.Application;

namespace Aether.Api;

public static class NotificationEndpoints
{
    public static void MapNotifications(this WebApplication app)
    {
        var group = app.MapGroup("/v1").RequireAuthorization();
        group.MapPost("/notifications", async (SubmitNotificationRequest body,
            HttpContext context, SubmitNotification handler, CancellationToken cancellationToken) =>
        {
            var tenantId = (Guid)context.Items["TenantId"]!;
            var correlationId = (Guid)context.Items["CorrelationId"]!;
            if (body.DeliveryMode != "fan_out" || body.Messages is not { Length: 1 }
                || body.Messages[0] is not { Channel: "email", Content: not null }
                || body.Recipient?.Email == null)
                throw new NotificationFault("unsupported_message", 422);
            if (body.Priority == "critical" && !context.User.HasClaim("permission", "notifications:critical"))
                throw new NotificationFault("priority_not_allowed", 403);
            if (!context.User.HasClaim("permission", "notifications:send"))
                throw new NotificationFault("permission_denied", 403);
            var content = body.Messages[0].Content;
            var draft = new NotificationDraft(body.Recipient.Email, content.Subject ?? "",
                content.Text, content.Html, body.Priority, body.ExpiresAt,
                body.ClientReference, body.Metadata?.GetRawText() ?? "{}");
            var acceptance = await handler.ExecuteAsync(tenantId, correlationId,
                context.Request.Headers["X-Idempotency-Key"].ToString(), draft, cancellationToken);
            var path = "/v1/notifications/" + acceptance.NotificationId;
            context.Response.Headers["Idempotency-Replayed"] = acceptance.Replayed ? "true" : "false";
            return Results.Accepted(path, new NotificationAcceptedResponse(acceptance.NotificationId,
                "accepted", acceptance.AcceptedAt, 1, acceptance.IdempotencyExpiresAt,
                new NotificationLinks(path, path + "/dispatches")));
        });
        group.MapGet("/notifications/{id:guid}", async (Guid id, HttpContext context,
            INotificationStore store, CancellationToken cancellationToken) =>
        {
            if (!context.User.HasClaim("permission", "notifications:read"))
                throw new NotificationFault("permission_denied", 403);
            var view = await store.GetAsync((Guid)context.Items["TenantId"]!, id, cancellationToken);
            if (view == null) return Results.NotFound();
            return Results.Ok(new NotificationStatusResponse(view.Request.Id,
                Summarize(view.Dispatches), view.Request.AcceptedAt, view.Request.ExpiresAt,
                view.Dispatches.Select(d => new DispatchStatusResponse(d.Id, d.Channel,
                    ToSnakeCase(d.State.ToString()), d.DeliveryState)).ToArray()));
        });
        group.MapGet("/notifications/{id:guid}/dispatches", async (Guid id, HttpContext context,
            INotificationStore store, CancellationToken cancellationToken) =>
        {
            if (!context.User.HasClaim("permission", "notifications:read"))
                throw new NotificationFault("permission_denied", 403);
            var view = await store.GetAsync((Guid)context.Items["TenantId"]!, id, cancellationToken);
            return view == null ? Results.NotFound() : Results.Ok(view.Dispatches.Select(d =>
                new DispatchStatusResponse(d.Id, d.Channel, ToSnakeCase(d.State.ToString()), d.DeliveryState)));
        });
    }

    private static string ToSnakeCase(string value) => string.Concat(value.Select((c, i) =>
        char.IsUpper(c) && i > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));

    private static string Summarize(Aether.Notifications.Domain.Dispatch[] dispatches)
    {
        if (dispatches.All(d => d.State == Aether.Notifications.Domain.ExecutionState.Pending)) return "accepted";
        if (dispatches.Any(d => d.State is Aether.Notifications.Domain.ExecutionState.Pending
            or Aether.Notifications.Domain.ExecutionState.InFlight or Aether.Notifications.Domain.ExecutionState.RetryScheduled
            or Aether.Notifications.Domain.ExecutionState.Scheduled or Aether.Notifications.Domain.ExecutionState.BlockedByPolicy
            or Aether.Notifications.Domain.ExecutionState.OutcomeUnknown)) return "processing";
        if (dispatches.All(d => d.State == Aether.Notifications.Domain.ExecutionState.Cancelled)) return "cancelled";
        var accepted = dispatches.Count(d => d.State == Aether.Notifications.Domain.ExecutionState.ProviderAccepted);
        return accepted == dispatches.Length ? "completed" : accepted > 0 ? "partially_completed" : "failed";
    }
}
