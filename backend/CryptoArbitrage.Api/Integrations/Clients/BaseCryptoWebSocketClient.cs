using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using CryptoArbitrage.Api.Core.Enums;
using CryptoArbitrage.Api.Core.Models;
using CryptoArbitrage.Api.Engine;
using CryptoArbitrage.Api.Infrastructure.Cache;
using CryptoArbitrage.Api.Integrations.Adapters;

namespace CryptoArbitrage.Api.Integrations.Clients;

// This base class manages the WebSocket lifecycle, reconnect logic,
// and publishing normalized order books into the processing pipeline.
public abstract class BaseCryptoWebSocketClient(
    ILogger logger,
    IRedisCacheService redisCache,
    IMarketDataHealthService health,
    Channel<NormalizedOrderBook> marketDataChannel) : BackgroundService, IExchangeOrderBookAdapter
{
    private const int ReceiveBufferSize = 8192;
    private static readonly TimeSpan MinReconnectDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxReconnectDelay = TimeSpan.FromSeconds(60);

    private readonly Dictionary<string, (decimal BestBid, decimal BestAsk)> _lastPrices = new(StringComparer.Ordinal);
    private readonly Channel<NormalizedOrderBook> _redisPublishQueue = Channel.CreateBounded<NormalizedOrderBook>(
        new BoundedChannelOptions(4096)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });

    private readonly SemaphoreSlim _sendLock = new(initialCount: 1, maxCount: 1);
    private string? _requestedReconnectReason;

    public string ExchangeName => GetType().Name.Replace("WebSocketClient", "", StringComparison.Ordinal);
    public abstract IReadOnlyCollection<string> Symbols { get; }

    protected abstract Uri GetWebSocketUrl();
    
    protected abstract void ProcessMessage(ReadOnlySpan<byte> jsonBytes);
    
    protected virtual Task SubscribeAsync(ClientWebSocket ws, CancellationToken ct) => Task.CompletedTask;

    protected virtual TimeSpan PingInterval => TimeSpan.Zero;
    protected virtual TimeSpan ReceiveIdleTimeout => TimeSpan.FromSeconds(45);
    protected virtual int MaxMessageBytes => 1024 * 1024;
    protected virtual string? GetRawPingMessage() => null;
    protected virtual object? GetPingPayload() => null;

    protected void RequestReconnect(string reason)
    {
        _requestedReconnectReason = reason;
    }

    protected void MarkSymbolRecovering(string symbol, string reason)
    {
        health.SetSymbolStatus(ExchangeName, symbol, MarketDataConnectionStatus.Recovering, reason);
    }

    protected async Task SendSubscriptionAsync(ClientWebSocket ws, object payload, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        await SendTextAsync(ws, bytes, ct).ConfigureAwait(false);
    }

    protected void PublishOrderBookIfChanged(string exchange, NormalizedOrderBook normalized)
    {
        if (_lastPrices.TryGetValue(normalized.Symbol, out var lastPrice) &&
            lastPrice.BestBid == normalized.BestBidPrice &&
            lastPrice.BestAsk == normalized.BestAskPrice)
        {
            return;
        }

        _lastPrices[normalized.Symbol] = (normalized.BestBidPrice, normalized.BestAskPrice);
        health.MarkOrderBook(exchange, normalized.Symbol);

        if (!marketDataChannel.Writer.TryWrite(normalized))
        {
            logger.LogWarning("[{Exchange}] In-memory market data queue is full, dropping {Symbol}", exchange, normalized.Symbol);
        }

        if (!_redisPublishQueue.Writer.TryWrite(normalized))
        {
            logger.LogWarning("[{Exchange}] Redis publish queue is closed, dropping {Symbol}", exchange, normalized.Symbol);
        }

        logger.LogInformation("[{Exchange}] {Symbol} | Top Bid: {Bid:0.00######} | Top Ask: {Ask:0.00######}",
            exchange, normalized.Symbol, normalized.BestBidPrice, normalized.BestAskPrice);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var redisPublisherTask = RunRedisPublisherLoopAsync(stoppingToken);

        try
        {
            await RunWebSocketLoopAsync(stoppingToken).ConfigureAwait(false);
        }
        finally
        {
            _redisPublishQueue.Writer.TryComplete();

            try
            {
                await redisPublisherTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
        }
    }

    private async Task RunWebSocketLoopAsync(CancellationToken stoppingToken)
    {
        var exchangeName = ExchangeName;
        var reconnectAttempts = 0;
        var consecutiveFailures = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            using var ws = new ClientWebSocket();
            using var pingCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            Task? pingTask = null;
            _requestedReconnectReason = null;

            try
            {
                var url = GetWebSocketUrl();
                health.SetExchangeStatus(exchangeName, MarketDataConnectionStatus.Connecting, reconnectAttempts: reconnectAttempts, consecutiveFailures: consecutiveFailures);
                SetAllSymbolStatuses(MarketDataConnectionStatus.Connecting);
                logger.LogInformation("[{Exchange}] Connecting to {Url}...", exchangeName, url);

                await ws.ConnectAsync(url, stoppingToken).ConfigureAwait(false);
                health.SetExchangeStatus(exchangeName, MarketDataConnectionStatus.Connected, reconnectAttempts: reconnectAttempts, consecutiveFailures: consecutiveFailures);
                logger.LogInformation("[{Exchange}] Successfully connected.", exchangeName);

                await SubscribeAsync(ws, stoppingToken).ConfigureAwait(false);
                pingTask = RunPingLoopAsync(ws, pingCts.Token);

                await ReceiveLoopAsync(exchangeName, ws, stoppingToken).ConfigureAwait(false);
                consecutiveFailures++;
                health.SetExchangeStatus(exchangeName, MarketDataConnectionStatus.Recovering, "Socket closed.", reconnectAttempts, consecutiveFailures);
                SetAllSymbolStatuses(MarketDataConnectionStatus.Recovering, "Socket closed.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                health.SetExchangeStatus(exchangeName, MarketDataConnectionStatus.Stopped);
                SetAllSymbolStatuses(MarketDataConnectionStatus.Stopped);
                break;
            }
            catch (Exception ex)
            {
                consecutiveFailures++;
                health.SetExchangeStatus(exchangeName, MarketDataConnectionStatus.Recovering, ex.Message, reconnectAttempts, consecutiveFailures);
                SetAllSymbolStatuses(MarketDataConnectionStatus.Recovering, ex.Message);
                logger.LogError(ex, "[{Exchange}] WebSocket error. Reconnecting...", exchangeName);
            }
            finally
            {
                await pingCts.CancelAsync().ConfigureAwait(false);

                if (pingTask is not null)
                {
                    try
                    {
                        await pingTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "[{Exchange}] Ping loop stopped with error.", exchangeName);
                    }
                }
            }

            reconnectAttempts++;
            var reconnectDelay = CalculateReconnectDelay(consecutiveFailures);
            health.SetExchangeStatus(
                exchangeName,
                MarketDataConnectionStatus.Recovering,
                reconnectAttempts: reconnectAttempts,
                consecutiveFailures: consecutiveFailures,
                currentBackoff: reconnectDelay);

            try
            {
                await Task.Delay(reconnectDelay, stoppingToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                health.SetExchangeStatus(exchangeName, MarketDataConnectionStatus.Stopped);
                SetAllSymbolStatuses(MarketDataConnectionStatus.Stopped);
                break;
            }
        }
    }

    private async Task ReceiveLoopAsync(string exchangeName, ClientWebSocket ws, CancellationToken stoppingToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(ReceiveBufferSize);

        try
        {
            using var ms = new MemoryStream(ReceiveBufferSize);

            while (ws.State == WebSocketState.Open && !stoppingToken.IsCancellationRequested)
            {
                ms.SetLength(0);

                WebSocketReceiveResult result;
                do
                {
                    result = await ReceiveWithTimeoutAsync(ws, buffer, stoppingToken).ConfigureAwait(false);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        logger.LogWarning("[{Exchange}] Connection closed by remote host.", exchangeName);
                        await CloseSocketAsync(ws, stoppingToken).ConfigureAwait(false);
                        return;
                    }

                    if (result.MessageType != WebSocketMessageType.Text)
                    {
                        continue;
                    }

                    if (ms.Length + result.Count > MaxMessageBytes)
                    {
                        throw new InvalidDataException($"[{exchangeName}] WebSocket message exceeded {MaxMessageBytes} bytes.");
                    }

                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (ms.Length == 0)
                {
                    continue;
                }

                DispatchMessage(ms, exchangeName);

                if (_requestedReconnectReason is not null)
                {
                    throw new IOException(_requestedReconnectReason);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private void DispatchMessage(MemoryStream ms, string exchangeName)
    {
        health.MarkMessage(exchangeName);
        var span = new ReadOnlySpan<byte>(ms.GetBuffer(), 0, (int)ms.Length);
        ProcessMessage(span);
    }

    private async Task<WebSocketReceiveResult> ReceiveWithTimeoutAsync(
        ClientWebSocket ws,
        byte[] buffer,
        CancellationToken stoppingToken)
    {
        var receiveTask = ws.ReceiveAsync(new ArraySegment<byte>(buffer), stoppingToken);

        try
        {
            return await receiveTask.WaitAsync(ReceiveIdleTimeout, stoppingToken).ConfigureAwait(false);
        }
        catch (TimeoutException ex)
        {
            ws.Abort();

            try
            {
                await receiveTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            throw new IOException($"No WebSocket data received for {ReceiveIdleTimeout.TotalSeconds:0}s.", ex);
        }
    }

    private async Task CloseSocketAsync(ClientWebSocket ws, CancellationToken stoppingToken)
    {
        try
        {
            if (ws.State is WebSocketState.CloseReceived or WebSocketState.Open)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (WebSocketException)
        {
            ws.Abort();
        }
    }

    private async Task SendTextAsync(ClientWebSocket ws, byte[] bytes, CancellationToken ct)
    {
        await _sendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task RunPingLoopAsync(ClientWebSocket ws, CancellationToken ct)
    {
        if (PingInterval == TimeSpan.Zero) return;

        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(PingInterval, ct).ConfigureAwait(false);

                var raw = GetRawPingMessage();
                if (raw != null)
                {
                    await SendTextAsync(ws, Encoding.UTF8.GetBytes(raw), ct).ConfigureAwait(false);
                    continue;
                }

                var payload = GetPingPayload();
                if (payload != null)
                {
                    await SendSubscriptionAsync(ws, payload, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "WebSocket ping failed.");
                break;
            }
        }
    }

    private async Task RunRedisPublisherLoopAsync(CancellationToken stoppingToken)
    {
        await foreach (var orderBook in _redisPublishQueue.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await redisCache.SetOrderBookAsync(orderBook).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[{Exchange}] Redis publish failed for {Symbol}", orderBook.Exchange, orderBook.Symbol);
            }
        }
    }

    private static TimeSpan CalculateReconnectDelay(int consecutiveFailures)
    {
        var exponent = Math.Min(Math.Max(consecutiveFailures - 1, 0), 6);
        var delayMs = MinReconnectDelay.TotalMilliseconds * Math.Pow(2, exponent);
        var boundedDelayMs = Math.Min(delayMs, MaxReconnectDelay.TotalMilliseconds);
        var jitterMs = Random.Shared.Next(0, 250);

        return TimeSpan.FromMilliseconds(boundedDelayMs + jitterMs);
    }

    private void SetAllSymbolStatuses(MarketDataConnectionStatus status, string? error = null)
    {
        foreach (var symbol in Symbols)
        {
            health.SetSymbolStatus(ExchangeName, symbol, status, error);
        }
    }
}