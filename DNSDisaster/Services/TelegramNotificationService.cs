using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using DNSDisaster.Models;

namespace DNSDisaster.Services;

public interface ITelegramNotificationService
{
    Task SendNotificationAsync(string message);
    Task SendFailoverNotificationAsync(string domain, string backupDomain);
    Task SendRecoveryNotificationAsync(string domain);
    Task SendErrorNotificationAsync(string error);
}

public class TelegramNotificationService : ITelegramNotificationService
{
    private readonly TelegramBotClient _botClient;
    private readonly TelegramSettings _settings;
    private readonly ILogger<TelegramNotificationService> _logger;

    public TelegramNotificationService(TelegramSettings settings, ILogger<TelegramNotificationService> logger)
    {
        _settings = settings;
        _logger = logger;
        _botClient = new TelegramBotClient(settings.BotToken);
    }

    public async Task SendNotificationAsync(string message)
    {
        try
        {
            // 支持数字ID和@username格式
            var chatId = long.TryParse(_settings.ChatId, out var numericId) 
                ? new ChatId(numericId) 
                : new ChatId(_settings.ChatId);

            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: $"🔔 DNS灾难恢复系统通知\n\n{message}\n\n⏰ 时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}"
            );
            _logger.LogInformation("Telegram通知发送成功: {Message}", message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送Telegram通知失败: {Message}", message);
        }
    }

    public async Task SendFailoverNotificationAsync(string domain, string backupDomain)
    {
        var message = $"⚠️ 故障转移触发\n\n" +
                     $"主域名: {domain}\n" +
                     $"备用域名: {backupDomain}\n" +
                     $"状态: 已切换到CNAME记录";
        
        await SendNotificationAsync(message);
    }

    public async Task SendRecoveryNotificationAsync(string domain)
    {
        var message = $"✅ 服务恢复\n\n" +
                     $"域名: {domain}\n" +
                     $"状态: 已恢复到A记录";
        
        await SendNotificationAsync(message);
    }

    public async Task SendErrorNotificationAsync(string error)
    {
        var message = $"❌ 系统错误\n\n" +
                     $"错误信息: {error}";
        
        await SendNotificationAsync(message);
    }
}