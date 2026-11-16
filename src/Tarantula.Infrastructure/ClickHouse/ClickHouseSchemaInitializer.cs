using ClickHouse.Client.ADO;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tarantula.Infrastructure.ClickHouse;

/// <summary>Creates the database and tables at startup. Retries because ClickHouse may still be starting.</summary>
internal sealed class ClickHouseSchemaInitializer(
    IHttpClientFactory httpClientFactory,
    IOptions<ClickHouseOptions> options,
    ILogger<ClickHouseSchemaInitializer> logger) : IHostedService
{
    private const int Attempts = 10;
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(3);

    private readonly ClickHouseOptions _options = options.Value;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await ApplyAsync(cancellationToken);
                logger.LogInformation("ClickHouse schema is ready (database {Database}).", _options.Database);
                return;
            }
            catch (Exception ex) when (attempt < Attempts && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "ClickHouse is not ready (attempt {Attempt}/{Attempts}); retrying.", attempt, Attempts);
                await Task.Delay(Delay, cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task ApplyAsync(CancellationToken cancellationToken)
    {
        using var connection = new ClickHouseConnection(
            _options.ConnectionString, httpClientFactory.CreateClient(ClickHouseEventStore.HttpClientName));
        await connection.OpenAsync(cancellationToken);

        foreach (var statement in ClickHouseSchema.Statements(_options))
        {
            using var command = connection.CreateCommand();
            command.CommandText = statement;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
