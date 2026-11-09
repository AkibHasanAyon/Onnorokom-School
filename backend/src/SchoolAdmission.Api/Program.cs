using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolAdmission.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// All application code reads time through TimeProvider; tests replace it with FakeTimeProvider.
builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

app.Run();

// Exposed for WebApplicationFactory<Program> in the integration tests.
public partial class Program { }
