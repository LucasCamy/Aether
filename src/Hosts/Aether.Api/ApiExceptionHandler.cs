using Aether.Notifications.Application;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Aether.Api;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code) = exception switch {
            NotificationFault fault => (fault.Status, fault.Code),
            BadHttpRequestException badRequest => (badRequest.StatusCode, "invalid_request"),
            NpgsqlException => (503, "persistence_unavailable"),
            DbUpdateException { InnerException: NpgsqlException } => (503, "persistence_unavailable"),
            _ => (500, "internal_error")
        };
        context.Response.StatusCode = status;
        await Results.Problem(statusCode: status, title: code,
            extensions: new Dictionary<string, object?> {
                ["code"] = code, ["correlationId"] = context.Items["CorrelationId"]
            }).ExecuteAsync(context);
        return true;
    }
}
