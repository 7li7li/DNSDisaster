using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using DNSDisaster.Models;

namespace DNSDisaster.Services;

public interface IIpProviderService
{
    Task<string?> GetCurrentIpAsync();
}

public class NyaTrpIpProviderService : IIpProviderService
{
    private readonly HttpClient _httpClient;
    private readonly IpProviderSettings _settings;
    private readonly ILogger<NyaTrpIpProviderService> _logger;
    private string? _cachedToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public NyaTrpIpProviderService(HttpClient httpClient, IpProviderSettings settings, ILogger<NyaTrpIpProviderService> logger)
    {
        _settings = settings;
        _logger = logger;
        
        // 创建自定义HttpClient来处理SSL问题
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
        _httpClient = new HttpClient(handler);
    }

    public async Task<string?> GetCurrentIpAsync()
    {
        try
        {
            // 如果配置了直接IP查询API，优先使用
            if (!string.IsNullOrEmpty(_settings.DirectIpApiUrl))
            {
                _logger.LogDebug("使用直接IP查询API: {ApiUrl}", _settings.DirectIpApiUrl);
                var directIp = await GetIpFromDirectApiAsync();
                if (!string.IsNullOrEmpty(directIp))
                {
                    _logger.LogInformation("通过直接API成功获取IP地址: {IpAddress}", directIp);
                    return directIp;
                }
                else
                {
                    _logger.LogWarning("直接API获取IP失败，回退到设备组API");
                }
            }

            // 回退到原有的设备组API方式
            return await GetIpFromDeviceGroupApiAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取当前IP时发生异常");
            return null;
        }
    }

    private async Task<string?> GetIpFromDirectApiAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(_settings.DirectIpApiUrl);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("直接IP查询API请求失败: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("直接IP查询API响应: {Content}", content);

            var ipResponse = JsonSerializer.Deserialize<DirectIpResponse>(content, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });

            if (!string.IsNullOrEmpty(ipResponse?.CurrentIp))
            {
                return ipResponse.CurrentIp;
            }

            _logger.LogError("直接IP查询API响应中未包含有效IP地址");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "调用直接IP查询API时发生异常");
            return null;
        }
    }

    private async Task<string?> GetIpFromDeviceGroupApiAsync()
    {
        try
        {
            // 获取或刷新token
            var token = await GetValidTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("无法获取有效的认证token");
                return null;
            }

            // 获取设备组信息
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_settings.ApiBaseUrl}/user/devicegroup");
            request.Headers.Add("Authorization", token);
            
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("获取设备组信息失败: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("设备组API响应: {Content}", content);
            
            var deviceGroupResponse = JsonSerializer.Deserialize<DeviceGroupResponse>(content, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                PropertyNameCaseInsensitive = true
            });

            if (deviceGroupResponse?.Data == null)
            {
                _logger.LogError("设备组响应数据为空");
                return null;
            }

            // 记录所有设备组信息用于调试
            _logger.LogInformation("找到 {Count} 个设备组:", deviceGroupResponse.Data.Length);
            foreach (var group in deviceGroupResponse.Data)
            {
                _logger.LogInformation("设备组 ID: {Id}, ConnectHost: '{ConnectHost}'", group.Id, group.ConnectHost ?? "null");
            }

            // 查找指定ID的设备组
            var targetGroup = deviceGroupResponse.Data.FirstOrDefault(g => g.Id == _settings.DeviceGroupId);
            if (targetGroup == null)
            {
                _logger.LogError("未找到ID为 {DeviceGroupId} 的设备组", _settings.DeviceGroupId);
                return null;
            }

            if (string.IsNullOrEmpty(targetGroup.ConnectHost))
            {
                _logger.LogError("设备组 {DeviceGroupId} 的 connect_host 为空", _settings.DeviceGroupId);
                return null;
            }

            // 提取IP地址，优先"电信"
            var ip = ExtractIpFromConnectHost(targetGroup.ConnectHost);
            if (!string.IsNullOrEmpty(ip))
            {
                _logger.LogInformation("成功获取IP地址: {IpAddress}", ip);
                return ip;
            }

            _logger.LogError("无法从 connect_host 中提取有效IP: {ConnectHost}", targetGroup.ConnectHost);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取当前IP时发生异常");
            return null;
        }
    }

    private async Task<string?> GetValidTokenAsync()
    {
        // 检查缓存的token是否仍然有效（提前5分钟刷新）
        if (!string.IsNullOrEmpty(_cachedToken) && DateTime.Now < _tokenExpiry.AddMinutes(-5))
        {
            return _cachedToken;
        }

        try
        {
            var loginRequest = new
            {
                username = _settings.Username,
                password = _settings.Password
            };

            var json = JsonSerializer.Serialize(loginRequest);
            _logger.LogDebug("发送登录请求: {Json}", json);
            
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_settings.ApiBaseUrl}/auth/login", content);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("登录响应状态: {StatusCode}, 内容: {Content}", response.StatusCode, responseContent);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("登录失败: {StatusCode}, 响应: {Content}", response.StatusCode, responseContent);
                return null;
            }

            var loginResponse = JsonSerializer.Deserialize<LoginResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (string.IsNullOrEmpty(loginResponse?.Data))
            {
                _logger.LogError("登录响应中未包含有效token: {Content}", responseContent);
                return null;
            }

            _cachedToken = loginResponse.Data;
            _tokenExpiry = DateTime.Now.AddHours(1); // 假设token有效期1小时
            
            _logger.LogInformation("成功获取认证token");
            return _cachedToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取认证token时发生异常");
            return null;
        }
    }

    private string? ExtractIpFromConnectHost(string connectHost)
    {
        try
        {
            // 优先查找"电信"关键字后的IP
            if (connectHost.Contains("电信"))
            {
                var telecomMatch = System.Text.RegularExpressions.Regex.Match(
                    connectHost, @"电信\s+(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})");
                if (telecomMatch.Success)
                {
                    return telecomMatch.Groups[1].Value;
                }
            }

            // 如果没有找到电信IP，则提取第一个有效IP
            var ipMatch = System.Text.RegularExpressions.Regex.Match(
                connectHost, @"(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})");
            if (ipMatch.Success)
            {
                return ipMatch.Groups[1].Value;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提取IP地址时发生异常: {ConnectHost}", connectHost);
            return null;
        }
    }
}

// 响应模型类
public class LoginResponse
{
    public string? Data { get; set; }
}

public class DeviceGroupResponse
{
    public DeviceGroup[]? Data { get; set; }
}

public class DeviceGroup
{
    public int Id { get; set; }
    
    [JsonPropertyName("connect_host")]
    public string ConnectHost { get; set; } = string.Empty;
}

// 直接IP查询API响应模型
public class DirectIpResponse
{
    [JsonPropertyName("current_ip")]
    public string CurrentIp { get; set; } = string.Empty;
}