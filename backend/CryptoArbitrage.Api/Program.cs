using StackExchange.Redis;
using Serilog;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using CryptoArbitrage.Api.Configuration;
using CryptoArbitrage.Api.Core.Models;
using CryptoArbitrage.Api.Core.Interfaces;
using CryptoArbitrage.Api.Engine;
using CryptoArbitrage.Api.Infrastructure.Alerts;
using CryptoArbitrage.Api.Infrastructure.Cache;
using CryptoArbitrage.Api.Infrastructure.Persistence;
using CryptoArbitrage.Api.Infrastructure.Persistence.Entities;
using CryptoArbitrage.Api.Integrations.Clients;
using CryptoArbitrage.Api.Api.Hubs;

// This file wires up the ASP.NET Core application, dependency injection,
// background services, and the real-time SignalR endpoints.
var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ArbitrageConfig>(
    builder.Configuration.GetSection("ArbitrageSettings"));

builder.Services.Configure<TelegramConfig>(
    builder.Configuration.GetSection("TelegramSettings"));

builder.Services.AddSingleton<IArbitrageSettingsManager, ArbitrageSettingsManager>();

builder.Logging.ClearProviders(); 
builder.Host.UseSerilog((_, config) => 
{
    config.WriteTo.Console(
        outputTemplate: "{Timestamp:HH:mm:ss} | {Message}{NewLine}{Exception}"
    );
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
redisOptions.AbortOnConnectFail = false;
redisOptions.ConnectRetry = 3;
redisOptions.ConnectTimeout = 3000;
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisOptions));
builder.Services.AddSingleton<IRedisCacheService, RedisCacheService>();
builder.Services.AddSingleton<IMarketDataHealthService, MarketDataHealthService>();
builder.Services.AddSingleton<IOrderBookManager, OrderBookManager>();

builder.Services.AddSingleton(Channel.CreateBounded<NormalizedOrderBook>(
    new BoundedChannelOptions(8192) { SingleWriter = false, SingleReader = true, FullMode = BoundedChannelFullMode.DropOldest }));

builder.Services.AddSingleton(Channel.CreateBounded<ArbitrageOpportunityEvent>(
    new BoundedChannelOptions(1000) { SingleWriter = true, SingleReader = true, FullMode = BoundedChannelFullMode.DropOldest }));

builder.Services.AddSingleton(Channel.CreateBounded<ArbitrageEventEntity>(
    new BoundedChannelOptions(10000) { SingleWriter = true, SingleReader = true, FullMode = BoundedChannelFullMode.DropOldest }));

builder.Services.AddHostedService<BinanceWebSocketClient>();
builder.Services.AddHostedService<BybitWebSocketClient>();
builder.Services.AddHostedService<OkxWebSocketClient>();
builder.Services.AddHostedService<BitgetWebSocketClient>();
builder.Services.AddHostedService<GateIoWebSocketClient>();

builder.Services.AddSingleton<IArbitrageExecutor, PaperTradingExecutor>();

builder.Services.AddHostedService<ArbitrageService>();
builder.Services.AddHostedService<TelegramAlertService>();
builder.Services.AddHostedService<DatabaseWriterService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .SetIsOriginAllowed(_ => true)
            .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("CorsPolicy");
app.MapControllers();
app.MapHub<ArbitrageHub>("/hubs/arbitrage");
app.MapGet("/health/market-data", (IMarketDataHealthService health) => Results.Ok(health.GetSnapshots()));

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();