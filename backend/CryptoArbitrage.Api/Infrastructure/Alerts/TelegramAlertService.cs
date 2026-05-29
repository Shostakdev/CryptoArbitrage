using System.Net;
using System.Threading.Channels;
using CryptoArbitrage.Api.Configuration;
using CryptoArbitrage.Api.Core.Models;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace CryptoArbitrage.Api.Infrastructure.Alerts;

// This service forwards arbitrage alerts to Telegram when a valid bot configuration is present.
public class TelegramAlertService(
    ILogger<TelegramAlertService> logger,
    Channel<ArbitrageOpportunityEvent> channel,
    IOptions<TelegramConfig> config) : BackgroundService
{
    private const int WorkerCount = 2;

    private static readonly SemaphoreSlim RateLimitSemaphore = new(initialCount: 1, maxCount: 1);
    
    private readonly string _chatId = config.Value.ChatId;
    private readonly ITelegramBotClient? _botClient =
        string.IsNullOrWhiteSpace(config.Value.BotToken) || config.Value.BotToken == "BOT_TOKEN"
            ? null
            : new TelegramBotClient(config.Value.BotToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Telegram alert service started. Waiting for signals...");

        // Two workers allow the service to keep up with the alert channel without blocking the engine.
        var workers = new Task[WorkerCount];
        for (var i = 0; i < workers.Length; i++)
        {
            workers[i] = RunWorkerAsync(i + 1, stoppingToken);
        }

        await Task.WhenAll(workers).ConfigureAwait(false);
    }

    private async Task RunWorkerAsync(int workerId, CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var alert in channel.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                logger.LogWarning(
                    "[ARBITRAGE] {Symbol} | {Mode} | Buy {BuyEx} ({BuyPrice:0.00######}) -> Sell {SellEx} ({SellPrice:0.00######}) | Gross: {GrossProfit:0.00##} USDT | Fees: {Fees:0.00##} USDT | Net: {Profit:0.00##} USDT ({ProfitPct:0.####}%)",
                    alert.Symbol,
                    alert.SettlementMode,
                    alert.BuyExchange,
                    alert.BuyPrice,
                    alert.SellExchange,
                    alert.SellPrice,
                    alert.GrossProfit,
                    alert.TotalFeesUsdt,
                    alert.NetProfit,
                    alert.NetProfitPercent);

                if (_botClient == null || string.IsNullOrWhiteSpace(_chatId) || _chatId == "CHAT_ID")
                {
                    continue;
                }

                await SendTelegramMessageAsync(alert, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Telegram alert worker {WorkerId} crashed.", workerId);
            throw;
        }
    }

    private async Task SendTelegramMessageAsync(ArbitrageOpportunityEvent alert, CancellationToken stoppingToken)
    {
        await RateLimitSemaphore.WaitAsync(stoppingToken).ConfigureAwait(false);
        try
        {
            await _botClient!.SendMessage(
                chatId: _chatId,
                text: BuildMessage(alert),
                parseMode: ParseMode.Html,
                cancellationToken: stoppingToken).ConfigureAwait(false);

            // Small throttling avoids hitting Telegram rate limits during fast market spikes.
            await Task.Delay(100, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send Telegram alert for {Symbol}.", alert.Symbol);
        }
        finally
        {
            RateLimitSemaphore.Release();
        }
    }

    // This formats the alert into a compact HTML message for Telegram.
    private static string BuildMessage(ArbitrageOpportunityEvent alert) =>
        $"""
        <b>ARBITRAGE FOUND</b>

        <b>Pair:</b> {WebUtility.HtmlEncode(alert.Symbol)}
        <b>Mode:</b> {WebUtility.HtmlEncode(alert.SettlementMode)}
        <b>Buy:</b> {WebUtility.HtmlEncode(alert.BuyExchange)} at {alert.BuyPrice:0.00######}
        <b>Sell:</b> {WebUtility.HtmlEncode(alert.SellExchange)} at {alert.SellPrice:0.00######}

        <b>Trade Size:</b> {alert.TradeAmountUsdt:0.##} USDT
        <b>Gross Spread:</b> {alert.GrossSpreadPercent:0.####}%
        <b>Gross Profit:</b> {alert.GrossProfit:0.00##} USDT
        <b>Fees:</b> {alert.TotalFeesUsdt:0.00##} USDT
        <b>Net Profit:</b> {alert.NetProfit:0.00##} USDT ({alert.NetProfitPercent:0.####}%)
        <b>Coins:</b> bought {alert.CoinsBought:0.########}, sold {alert.CoinsSold:0.########}
        <b>Time:</b> {alert.Timestamp:HH:mm:ss} UTC
        """;
}
