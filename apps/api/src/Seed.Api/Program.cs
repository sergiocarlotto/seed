var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// TLS is terminated by the reverse proxy (Caddy) per ADR-0007, so the API
// listens over plain HTTP inside the container. No HTTPS redirection here.

app.UseAuthorization();

app.MapControllers();

// Liveness endpoint used by Docker/Compose health checks (ADR-0007).
app.MapHealthChecks("/health");

app.Run();

// Exposed so integration tests can bootstrap the API via WebApplicationFactory.
public partial class Program { }
