using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using DNSDisaster.Models;

namespace DNSDisaster.Services;

public interface ISubscriptionMonitorService
{
    Task CheckSubscriptionStatusAsync(SubscriptionApiSettings settings, string taskName);
    Task StartMonitoringAsync(SubscriptionMonitorTask task);
}

public class SubscriptionMonitorService : ISubscriptionMonitorService
{
    private readonly HttpClient _httpClient;
    private readonly ITelegramNotificationService _telegramService;
    private readonly ILogger<SubscriptionMonitorService> _logger;
    private readonly Dictionary<string, DateTime> _lastNotificationTime = new();
    private readonly TimeSpan _notificationCooldown = TimeSpan.FromHours(24); // 24小时内不重复通知

    public SubscriptionMonitorService(
        HttpClient httpClient,
        ITelegramNotificationService telegramService,
        ILogger<SubscriptionMonitorService> logger)
    {
        _httpClient = httpClient;
        _telegramService = telegramService;
        _logger = logger;
        
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
        _httpClient = new HttpClient(handler);
    }

    public async Task StartMonitoringAsync(SubscriptionMonitorTask task)
    {
        _logger.LogInformation("[{TaskName}] 启动套餐监控服务，检查间隔: {Interval} 小时", task.Name, task.CheckIntervalHours);
        
        while (true)
        {
            try
            {
                await CheckSubscriptionStatusAsync(task.ApiSettings, task.Name);
                await Task.Delay(TimeSpan.FromHours(task.CheckIntervalHours));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{TaskName}] 套餐监控循环发生异常", task.Name);
                await Task.Delay(TimeSpan.FromMinutes(10)); // 出错后等待10分钟再重试
            }
        }
    }

    public async Task CheckSubscriptionStatusAsync(SubscriptionApiSettings settings, string taskName)
    {
        try
        {
            var token = await GetTokenAsync(settings);
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("[{TaskName}] 无法获取认证token，跳过套餐检查", taskName);
                return;
            }

            var userInfo = await GetUserInfoAsync(settings.ApiBaseUrl, token);
            if (userInfo == null)
            {
                _logger.LogError("[{TaskName}] 无法获取用户信息", taskName);
                return;
            }

            _logger.LogInformation("[{TaskName}] 套餐状态 - 用户: {Username}, 套餐: {PlanName}, 到期时间: {ExpireTime}, 流量: {UsedTraffic:F2}/{TotalTraffic:F2} GB ({UsagePercent:F1}%), 余额: {Balance:F2} 元",
                taskName,
                userInfo.Username,
                userInfo.PlanName,
                userInfo.ExpireTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "未知",
                userInfo.UsedTraffic,
                userInfo.TotalTraffic,
                userInfo.TotalTraffic > 0 ? (userInfo.UsedTraffic / userInfo.TotalTraffic * 100) : 0,
                userInfo.Balance);

            await CheckAndNotifyAsync(userInfo, taskName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{TaskName}] 检查套餐状态时发生异常", taskName);
        }
    }

    private async Task<string?> GetTokenAsync(SubscriptionApiSettings settings)
    {
        try
        {
            var loginRequest = new
            {
                username = settings.Username,
                password = settings.Password
            };

            var json = JsonSerializer.Serialize(loginRequest);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{settings.ApiBaseUrl}/auth/login", content);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("登录失败: {StatusCode}", response.StatusCode);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var loginResponse = JsonSerializer.Deserialize<LoginResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            return loginResponse?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取认证token时发生异常");
            return null;
        }
    }

    private async Task<UserInfo?> GetUserInfoAsync(string apiBaseUrl, string token)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{apiBaseUrl}/user/info");
            request.Headers.Add("Authorization", token);
            
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("获取用户信息失败: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("用户信息API响应: {Content}", content);
            
            var userInfoResponse = JsonSerializer.Deserialize<UserInfoResponse>(content, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });

            return userInfoResponse?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户信息时发生异常");
            return null;
        }
    }

    private async Task CheckAndNotifyAsync(UserInfo userInfo, string taskName)
    {
        var warnings = new List<string>();
        var isUrgent = false;

        // 检查套餐到期时间
        if (userInfo.ExpireTime.HasValue)
        {
            var daysUntilExpire = (userInfo.ExpireTime.Value - DateTime.Now).TotalDays;
            if (daysUntilExpire <= 7)
            {
                warnings.Add($"⏰ 套餐将在 {daysUntilExpire:F1} 天后到期（{userInfo.ExpireTime.Value:yyyy-MM-dd HH:mm:ss}）");
                isUrgent = true;
            }
            else if (daysUntilExpire <= 14)
            {
                warnings.Add($"⏰ 套餐将在 {daysUntilExpire:F1} 天后到期（{userInfo.ExpireTime.Value:yyyy-MM-dd HH:mm:ss}）");
            }
        }

        // 检查流量使用情况
        if (userInfo.TotalTraffic > 0)
        {
            var trafficUsagePercent = (userInfo.UsedTraffic / userInfo.TotalTraffic) * 100;
            if (trafficUsagePercent >= 90)
            {
                warnings.Add($"📊 流量已使用 {trafficUsagePercent:F1}% ({userInfo.UsedTraffic:F2}/{userInfo.TotalTraffic:F2} GB)");
                isUrgent = true;
            }
            else if (trafficUsagePercent >= 80)
            {
                warnings.Add($"📊 流量已使用 {trafficUsagePercent:F1}% ({userInfo.UsedTraffic:F2}/{userInfo.TotalTraffic:F2} GB)");
            }
        }

        // 检查余额是否足够续费
        if (userInfo.RenewalPrice > 0 && userInfo.Balance < userInfo.RenewalPrice)
        {
            warnings.Add($"💰 余额不足：当前余额 {userInfo.Balance:F2} 元，续费需要 {userInfo.RenewalPrice:F2} 元");
            isUrgent = true;
        }

        // 如果有警告且需要通知
        if (warnings.Count > 0 && isUrgent)
        {
            var notificationKey = $"{taskName}_subscription";
            
            // 检查是否在冷却期内
            if (_lastNotificationTime.TryGetValue(notificationKey, out var lastTime))
            {
                if (DateTime.Now - lastTime < _notificationCooldown)
                {
                    _logger.LogInformation("[{TaskName}] 套餐警告在冷却期内，跳过通知", taskName);
                    return;
                }
            }

            // 构建通知消息，始终包含流量信息
            var trafficInfo = userInfo.TotalTraffic > 0 
                ? $"📊 流量使用: {userInfo.UsedTraffic:F2}/{userInfo.TotalTraffic:F2} GB ({(userInfo.UsedTraffic / userInfo.TotalTraffic * 100):F1}%)"
                : "📊 流量使用: 无数据";

            var message = $"⚠️ 套餐状态警告 [{taskName}]\n" +
                         $"👤 用户: {userInfo.Username}\n" +
                         $"📦 套餐: {userInfo.PlanName}\n" +
                         $"{trafficInfo}\n\n" + 
                         string.Join("\n", warnings);
            
            await _telegramService.SendNotificationAsync(message);
            
            _lastNotificationTime[notificationKey] = DateTime.Now;
            _logger.LogInformation("[{TaskName}] 已发送套餐警告通知", taskName);
        }
    }

    private class LoginResponse
    {
        public string? Data { get; set; }
    }

    private class UserInfoResponse
    {
        public UserInfo? Data { get; set; }
    }
}

public class UserInfo
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("plan_name")]
    public string PlanName { get; set; } = string.Empty;

    [JsonPropertyName("expire")]
    public long? ExpireTimestamp { get; set; }

    [JsonPropertyName("traffic_used")]
    public long TrafficUsedBytes { get; set; }

    [JsonPropertyName("traffic_enable")]
    public long TrafficEnableBytes { get; set; }

    [JsonPropertyName("balance")]
    public string BalanceString { get; set; } = "0";

    [JsonPropertyName("renew_price")]
    public string RenewPriceString { get; set; } = "0";

    // 计算属性
    [JsonIgnore]
    public DateTime? ExpireTime => ExpireTimestamp.HasValue 
        ? DateTimeOffset.FromUnixTimeSeconds(ExpireTimestamp.Value).LocalDateTime 
        : null;

    [JsonIgnore]
    public double UsedTraffic => TrafficUsedBytes / (1024.0 * 1024.0 * 1024.0); // 转换为 GB

    [JsonIgnore]
    public double TotalTraffic => TrafficEnableBytes / (1024.0 * 1024.0 * 1024.0); // 转换为 GB

    [JsonIgnore]
    public double Balance
    {
        get
        {
            if (double.TryParse(BalanceString, out var balance))
                return balance;
            return 0;
        }
    }

    [JsonIgnore]
    public double RenewalPrice
    {
        get
        {
            if (double.TryParse(RenewPriceString, out var price))
                return price;
            return 0;
        }
    }
}
