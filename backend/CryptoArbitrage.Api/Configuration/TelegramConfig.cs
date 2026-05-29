namespace CryptoArbitrage.Api.Configuration;

/// <summary>
/// Configuration values used to authenticate and target the Telegram alert bot.
/// </summary>
public class TelegramConfig
{
    /// <summary>
    /// Bot token that allows the backend to send messages on behalf of the Telegram bot.
    /// </summary>
    public string BotToken { get; init; } = string.Empty;

    /// <summary>
    /// Destination chat identifier that receives the bot notifications.
    /// </summary>
    public string ChatId { get; init; } = string.Empty;
}