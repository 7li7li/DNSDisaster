using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
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
    private readonly HttpClient _httpClient;
    private readonly TelegramSettings _settings;
    private readonly ILogger<TelegramNotificationService> _logger;
    private readonly string _apiUrl;

    public TelegramNotificationService(HttpClient httpClient, TelegramSettings settings, ILogger<TelegramNotificationService> logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
        
        // 构建完整的API URL
        var baseUrl = settings.ApiBaseUrl.TrimEnd('/');
        _apiUrl = $"{baseUrl}/bot{settings.BotToken}";
        
        _logger.LogInformation("Telegram Bot 初始化完成，使用API地址: {ApiBaseUrl}", baseUrl);
    }

    public async Task SendNotificationAsync(string message)
    {
        try
        {
            var fullMessage = $"🔔 DNS灾难恢复系统通知\n\n{message}\n\n⏰ 时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            
            var requestBody = new
            {
                chat_id = _settings.ChatId,
                text = fullMessage,
                parse_mode = "HTML"
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_apiUrl}/sendMessage", content);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Telegram通知发送成功: {Message}", message);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Telegram通知发送失败: {StatusCode}, {Content}", response.StatusCode, errorContent);
            }
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