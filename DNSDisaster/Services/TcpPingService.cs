using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace DNSDisaster.Services;

public interface ITcpPingService
{
    Task<bool> PingAsync(string host, int port, int timeoutMs = 5000);
}

public class TcpPingService : ITcpPingService
{
    private readonly ILogger<TcpPingService> _logger;

    public TcpPingService(ILogger<TcpPingService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> PingAsync(string host, int port, int timeoutMs = 5000)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            var timeoutTask = Task.Delay(timeoutMs);

            var completedTask = await Task.WhenAny(connectTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                _logger.LogWarning("TCP连接到 {Host}:{Port} 超时", host, port);
                return false;
            }

            if (connectTask.IsFaulted)
            {
                _logger.LogWarning("TCP连接到 {Host}:{Port} 失败: {Error}", host, port, connectTask.Exception?.GetBaseException().Message);
                return false;
            }

            _logger.LogDebug("TCP连接到 {Host}:{Port} 成功", host, port);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TCP连接到 {Host}:{Port} 时发生异常", host, port);
            return false;
        }
    }
}