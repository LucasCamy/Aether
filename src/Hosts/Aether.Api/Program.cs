var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();
app.MapGet("/health/live", () => Results.Ok(new { status = "alive", service = "Aether.Api" }));

app.Run();
