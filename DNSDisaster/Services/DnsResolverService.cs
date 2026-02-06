using System.Net;
using Microsoft.Extensions.Logging;

namespace DNSDisaster.Services;

public interface IDnsResolverService
{
    Task<string?> GetARecordAsync(string domain);
    Task<bool> IsARecordAsync(string domain);
}

public class DnsResolverService : IDnsResolverService
{
    private readonly ICloudflareService _cloudflareService;
    private readonly ILogger<DnsResolverService> _logger;

    public DnsResolverService(ICloudflareService cloudflareService, ILogger<DnsResolverService> logger)
    {
        _cloudflareService = cloudflareService;
        _logger = logger;
    }

    public async Task<string?> GetARecordAsync(string domain)
    {
        try
        {
            // 通过Cloudflare API获取当前DNS记录（即时生效，无缓存）
            var recordType = await _cloudflareService.GetCurrentRecordTypeAsync();
            var recordContent = await _cloudflareService.GetCurrentRecordContentAsync();

            if (string.IsNullOrEmpty(recordType) || string.IsNullOrEmpty(recordContent))
            {
                _logger.LogWarning("无法从Cloudflare获取域名 {Domain} 的DNS记录", domain);
                return null;
            }

            // 如果是A记录，返回IP地址
            if (recordType.Equals("A", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("域名 {Domain} 当前为A记录，IP: {IpAddress}", domain, recordContent);
                return recordContent;
            }
            
            // 如果是CNAME记录，返回null（表示不是A记录）
            if (recordType.Equals("CNAME", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("域名 {Domain} 当前为CNAME记录，目标: {Target}", domain, recordContent);
                return null;
            }

            _logger.LogWarning("域名 {Domain} 的记录类型为 {RecordType}，不是A记录", domain, recordType);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "通过Cloudflare API解析域名 {Domain} 时发生异常", domain);
            return null;
        }
    }

    public async Task<bool> IsARecordAsync(string domain)
    {
        try
        {
            var recordType = await _cloudflareService.GetCurrentRecordTypeAsync();
            return recordType?.Equals("A", StringComparison.OrdinalIgnoreCase) ?? false;
        }
        catch
        {
            return false;
        }
    }
}