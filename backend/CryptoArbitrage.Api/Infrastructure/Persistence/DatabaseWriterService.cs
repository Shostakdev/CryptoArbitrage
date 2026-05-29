using System.Threading.Channels;
using CryptoArbitrage.Api.Infrastructure.Persistence.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoArbitrage.Api.Infrastructure.Persistence;

// This background service writes arbitrage events into PostgreSQL in a separate worker loop.
public class DatabaseWriterService : BackgroundService
{
    private readonly ChannelReader<ArbitrageEventEntity> _channelReader;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseWriterService> _logger;

    public DatabaseWriterService(
        Channel<ArbitrageEventEntity> channel,
        IServiceScopeFactory scopeFactory,
        ILogger<DatabaseWriterService> logger)
    {
        _channelReader = channel.Reader;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Database Writer Service started.");

        // This loop keeps the database write path isolated from the real-time calculation path.
        await foreach (var entity in _channelReader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                dbContext.SpreadHistory.Add(entity);
                await dbContext.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write arbitrage event {Symbol} to DB", entity.Symbol);
            }
        }
    }
}