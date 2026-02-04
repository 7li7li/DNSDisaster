using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using DNSDisaster.Models;

namespace DNSDisaster.Services;

public interface ICloudflareService
{
    Task<bool> SwitchToCnameAsync(string targetDomain);
    Task<bool> SwitchToARecordAsync(string ipAddress);
    Task<string?> GetCurrentRecordTypeAsync();
}

public class CloudflareDnsService : ICloudflareService
{
    private readonly HttpClient _httpClient;
    private readonly CloudflareSettings _settings;
    private readonly string _recordName;
    private readonly ILogger<CloudflareDnsService> _logger;

    public CloudflareDnsService(HttpClient httpClient, CloudflareSettings settings, DNSDisasterSettings dnsSettings, ILogger<CloudflareDnsService> logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _recordName = dnsSettings.PrimaryDomain;
        _logger = logger;
        
        _httpClient.BaseAddress = new Uri("https://api.cloudflare.com/client/v4/");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {settings.ApiToken}");
    }

    public async Task<bool> SwitchToCnameAsync(string targetDomain)
    {
        try
        {
            // 获取现有记录
            var existingRecord = await GetDnsRecordAsync();
            
            if (existingRecord != null)
            {
                // 删除现有记录
                await DeleteDnsRecordAsync(existingRecord.Id);
                _logger.LogInformation("已删除现有DNS记录: {RecordName}", _recordName);
            }

            // 创建CNAME记录
            var cnameRecord = new
            {
                type = "CNAME",
                name = _recordName,
                content = targetDomain,
                ttl = 300
            };

            var success = await CreateDnsRecordAsync(cnameRecord);
            
            if (success)
            {
                _logger.LogInformation("成功切换到CNAME记录: {RecordName} -> {TargetDomain}", _recordName, targetDomain);
                return true;
            }
            else
            {
                _logger.LogError("切换到CNAME记录失败");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换到CNAME记录时发生异常");
            return false;
        }
    }

    public async Task<bool> SwitchToARecordAsync(string ipAddress)
    {
        try
        {
            // 获取现有记录
            var existingRecord = await GetDnsRecordAsync();
            
            if (existingRecord != null)
            {
                // 删除现有记录
                await DeleteDnsRecordAsync(existingRecord.Id);
                _logger.LogInformation("已删除现有DNS记录: {RecordName}", _recordName);
            }

            // 创建A记录
            var aRecord = new
            {
                type = "A",
                name = _recordName,
                content = ipAddress,
                ttl = 300
            };

            var success = await CreateDnsRecordAsync(aRecord);
            
            if (success)
            {
                _logger.LogInformation("成功切换到A记录: {RecordName} -> {IpAddress}", _recordName, ipAddress);
                return true;
            }
            else
            {
                _logger.LogError("切换到A记录失败");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换到A记录时发生异常");
            return false;
        }
    }

    public async Task<string?> GetCurrentRecordTypeAsync()
    {
        try
        {
            var record = await GetDnsRecordAsync();
            return record?.Type;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取当前DNS记录类型时发生异常");
            return null;
        }
    }

    private async Task<DnsRecord?> GetDnsRecordAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"zones/{_settings.ZoneId}/dns_records?name={_recordName}");
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("获取DNS记录失败: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<CloudflareResponse<DnsRecord[]>>(content, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            return result?.Result?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取DNS记录时发生异常");
            return null;
        }
    }

    private async Task<bool> DeleteDnsRecordAsync(string recordId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"zones/{_settings.ZoneId}/dns_records/{recordId}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除DNS记录时发生异常");
            return false;
        }
    }

    private async Task<bool> CreateDnsRecordAsync(object record)
    {
        try
        {
            var json = JsonSerializer.Serialize(record, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"zones/{_settings.ZoneId}/dns_records", content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("创建DNS记录失败: {StatusCode}, {Content}", response.StatusCode, errorContent);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建DNS记录时发生异常");
            return false;
        }
    }
}

public class CloudflareResponse<T>
{
    public bool Success { get; set; }
    public T? Result { get; set; }
    public CloudflareError[]? Errors { get; set; }
}

public class CloudflareError
{
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class DnsRecord
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Ttl { get; set; }
}