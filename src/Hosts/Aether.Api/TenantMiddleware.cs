using Aether.Notifications.Application;

namespace Aether.Api;

public sealed class TenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/v1"))
        {
            if (!Guid.TryParse(context.Request.Headers["X-Correlation-ID"], out var correlation) || correlation == Guid.Empty)
                throw new NotificationFault("invalid_correlation_id", 400);
            context.Items["CorrelationId"] = correlation;
            context.Response.Headers["X-Correlation-ID"] = correlation.ToString("D");
            if (!Guid.TryParse(context.Request.Headers["X-Tenant-ID"], out var tenant) || tenant == Guid.Empty)
                throw new NotificationFault("invalid_tenant_id", 400);
            if (!context.User.FindAll("tenant_id").Any(claim =>
                Guid.TryParse(claim.Value, out var authorized) && authorized == tenant))
                throw new NotificationFault("tenant_not_authorized", 403);
            context.Items["TenantId"] = tenant;
        }
        await next(context);
    }
}
