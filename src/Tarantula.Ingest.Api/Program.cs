using System.Threading.RateLimiting;
using Tarantula.Application;
using Tarantula.Infrastructure;
using Tarantula.Ingest.Api;
using Tarantula.Ingest.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// The collect endpoint is public by nature (it is called from visitors' browsers), so any origin may call it.
// Which origins may write to which project is enforced by the use case, using the project's allow list.
builder.Services.AddCors(options => options.AddPolicy(Program.CollectCorsPolicy, policy => policy
    .AllowAnyOrigin()
    .AllowAnyHeader()
    .WithMethods("POST", "OPTIONS")
    .SetPreflightMaxAge(TimeSpan.FromHours(1))));

// Reading is protected by a secret key, so any origin may call it from a dashboard during development.
builder.Services.AddCors(options => options.AddPolicy(Program.ReadCorsPolicy, policy => policy
    .AllowAnyOrigin()
    .AllowAnyHeader()
    .WithMethods("GET", "OPTIONS")));

var requestsPerMinute = builder.Configuration.GetValue("Tarantula:Ingest:RequestsPerMinute", 600);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(Program.CollectRateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = requestsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

    // A stream is one request that lasts, so this only limits how fast clients may (re)connect.
    options.AddPolicy(Program.ReadRateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = 512 * 1024);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    ProductionGuard.EnsureNoDevelopmentSecrets(app.Services);
}

app.UseCors();
app.UseRateLimiter();
app.MapCollect();
app.MapLive();
app.MapBusiness();

app.Run();

/// <summary>Marker type so integration tests can start the app with WebApplicationFactory.</summary>
public partial class Program
{
    internal const string CollectCorsPolicy = "collect";
    internal const string CollectRateLimitPolicy = "collect";
    internal const string ReadCorsPolicy = "read";
    internal const string ReadRateLimitPolicy = "read";
}
