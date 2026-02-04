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
    private readonly ILogger<DnsResolverService> _logger;

    public DnsResolverService(ILogger<DnsResolverService> logger)
    {
        _logger = logger;
    }

    public async Task<string?> GetARecordAsync(string domain)
    {
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(domain);
            var ipv4Address = addresses.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            
            if (ipv4Address != null)
            {
                _logger.LogDebug("域名 {Domain} 解析到IP: {IpAddress}", domain, ipv4Address);
                return ipv4Address.ToString();
            }
            
            _logger.LogWarning("域名 {Domain} 未找到IPv4地址", domain);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析域名 {Domain} 时发生异常", domain);
            return null;
        }
    }

    public async Task<bool> IsARecordAsync(string domain)
    {
        try
        {
            // 简单检查：如果能解析到IP地址，通常表示是A记录
            // 更准确的方法需要使用DNS查询库，但这里用简单方法
            var ip = await GetARecordAsync(domain);
            return !string.IsNullOrEmpty(ip);
        }
        catch
        {
            return false;
        }
    }
}