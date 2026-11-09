using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolAdmission.Api.ErrorHandling;
using SchoolAdmission.Api.Json;
using SchoolAdmission.Application;
using SchoolAdmission.Infrastructure;
using SchoolAdmission.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// All application code reads time through TimeProvider; tests replace it with FakeTimeProvider.
builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new UtcDateTimeOffsetConverter());
        // The web defaults accept numbers in strings ("120"); the contract requires a JSON integer → 400.
        o.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    });
builder.Services.Configure<ApiBehaviorOptions>(o =>
    o.InvalidModelStateResponseFactory = ProblemDetailsSetup.FromInvalidModelState);
builder.Services.AddProblemDetails(ProblemDetailsSetup.Customize);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// One structured line per request: method, path, status, duration. Never headers (X-Student-Id) or bodies.
builder.Services.AddHttpLogging(o =>
{
    o.LoggingFields = HttpLoggingFields.RequestMethod | HttpLoggingFields.RequestPath
                    | HttpLoggingFields.ResponseStatusCode | HttpLoggingFields.Duration;
    o.CombineLogs = true;
});

// The browser calls the API directly (http://localhost:8080), so the SPA origin must be allowed.
var origins = (builder.Configuration["CORS_ALLOWED_ORIGINS"] ?? "http://localhost:5173,http://127.0.0.1:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddPolicy("frontend", p => p
    .WithOrigins(origins)
    .WithHeaders("Content-Type", "X-Student-Id")
    .WithMethods("GET", "POST", "DELETE")
    .WithExposedHeaders("Location")));

builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 64 * 1024); // larger → 413

var app = builder.Build();

// Waits for PostgreSQL, then applies migrations under an advisory lock (safe with several instances).
await app.Services.MigrateDatabaseAsync(app.Lifetime.ApplicationStopping);

app.UseHttpLogging();     // outermost, so it records the final status code (including error responses)
app.UseExceptionHandler();
app.UseStatusCodePages(); // empty 404/405/... responses get a problem+json body
app.UseCors("frontend");  // before MapControllers so error responses carry CORS headers too
app.MapControllers();

app.Run();

// Exposed for WebApplicationFactory<Program> in the integration tests.
public partial class Program { }
