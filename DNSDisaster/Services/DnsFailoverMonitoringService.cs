using Microsoft.Extensions.Logging;
using DNSDisaster.Models;

namespace DNSDisaster.Services;

public class DnsFailoverMonitoringService
{
    private readonly ITcpPingService _tcpPingService;
    private readonly ICloudflareService _cloudflareService;
    private readonly ITelegramNotificationService _telegramService;
    private readonly IDnsResolverService _dnsResolverService;
    private readonly DnsFailoverTask _task;
    private readonly ILogger<DnsFailoverMonitoringService> _logger;

    private int _consecutiveFailures = 0;
    private DnsRecordState _currentState = DnsRecordState.Unknown;
    private bool _hasCheckedDnsConsistency = false;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public DnsFailoverMonitoringService(
        ITcpPingService tcpPingService,
        ICloudflareService cloudflareService,
        ITelegramNotificationService telegramService,
        IDnsResolverService dnsResolverService,
        DnsFailoverTask task,
        ILogger<DnsFailoverMonitoringService> logger)
    {
        _tcpPingService = tcpPingService;
        _cloudflareService = cloudflareService;
        _telegramService = telegramService;
        _dnsResolverService = dnsResolverService;
        _task = task;
        _logger = logger;
    }

    public async Task StartMonitoringAsync()
    {
        _logger.LogInformation("[{TaskName}] 开始DNS容灾监控服务...", _task.Name);
        await _telegramService.SendNotificationAsync($"🚀 DNS容灾监控已启动\n任务: {_task.Name}\n域名: {_task.PrimaryDomain}\n目标IP: {_task.Ip}:{_task.PrimaryPort}");

        await MonitoringLoopAsync(_cancellationTokenSource.Token);
    }

    public void Stop()
    {
        _logger.LogInformation("[{TaskName}] 停止DNS容灾监控服务...", _task.Name);
        _cancellationTokenSource.Cancel();
    }

    private async Task MonitoringLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await PerformDnsMonitoringAsync();
                await Task.Delay(TimeSpan.FromSeconds(_task.CheckIntervalSeconds), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{TaskName}] 监控循环中发生异常", _task.Name);
                await _telegramService.SendErrorNotificationAsync($"[{_task.Name}] DNS容灾监控循环异常: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(_task.CheckIntervalSeconds), cancellationToken);
            }
        }
    }

    private async Task PerformDnsMonitoringAsync()
    {
        _logger.LogDebug("[{TaskName}] 检测固定IP连通性: {IpAddress}:{Port}", _task.Name, _task.Ip, _task.PrimaryPort);
        var isIpReachable = await _tcpPingService.PingAsync(_task.Ip, _task.PrimaryPort);

        if (isIpReachable)
        {
            _logger.LogDebug("[{TaskName}] 固定IP {IpAddress} 可达", _task.Name, _task.Ip);

            if (_consecutiveFailures > 0)
            {
                _logger.LogInformation("[{TaskName}] 连接恢复，重置失败计数", _task.Name);
                _consecutiveFailures = 0;
            }

            if (!_hasCheckedDnsConsistency)
            {
                _logger.LogInformation("[{TaskName}] 检测DNS一致性: IP={IpAddress}", _task.Name, _task.Ip);
                var domainIp = await _dnsResolverService.GetARecordAsync(_task.PrimaryDomain);

                if (string.IsNullOrEmpty(domainIp))
                {
                    _logger.LogDebug("[{TaskName}] 主域名 {Domain} 当前不是A记录，准备切换到A记录", _task.Name, _task.PrimaryDomain);
                    await SwitchToARecordAsync(_task.Ip, "主域名当前不是A记录");
                }
                else if (domainIp != _task.Ip)
                {
                    _logger.LogDebug("[{TaskName}] 主域名IP不一致: 域名={DomainIp}, 目标={TargetIp}", _task.Name, domainIp, _task.Ip);
                    await SwitchToARecordAsync(_task.Ip, $"IP不一致 ({domainIp} → {_task.Ip})");
                }
                else
                {
                    _logger.LogDebug("[{TaskName}] 主域名IP一致: {IpAddress}，无需更新", _task.Name, _task.Ip);
                    _currentState = DnsRecordState.ARecord;
                }

                _hasCheckedDnsConsistency = true;
            }

            return;
        }

        _consecutiveFailures++;
        _logger.LogWarning("[{TaskName}] ❌ 固定IP {IpAddress} 不可达 (失败 {FailureCount}/{Threshold})",
            _task.Name, _task.Ip, _consecutiveFailures, _task.FailureThreshold);

        if (_consecutiveFailures >= _task.FailureThreshold)
        {
            await SwitchToCnameAsync();
            _consecutiveFailures = 0;
        }
    }

    private async Task SwitchToARecordAsync(string ipAddress, string reason)
    {
        try
        {
            _logger.LogInformation("[{TaskName}] 准备切换到A记录: IP={IpAddress}, 原因={Reason}", _task.Name, ipAddress, reason);

            var success = await _cloudflareService.SwitchToARecordAsync(ipAddress);

            if (success)
            {
                _currentState = DnsRecordState.ARecord;
                await _telegramService.SendNotificationAsync(
                    $"✅ DNS容灾记录已更新\n\n" +
                    $"任务: {_task.Name}\n" +
                    $"域名: {_task.PrimaryDomain}\n" +
                    $"类型: A记录\n" +
                    $"IP: {ipAddress}\n" +
                    $"原因: {reason}");
                _logger.LogInformation("[{TaskName}] 成功切换到A记录: {IpAddress}", _task.Name, ipAddress);
            }
            else
            {
                _logger.LogError("[{TaskName}] 切换到A记录失败", _task.Name);
                await _telegramService.SendErrorNotificationAsync($"[{_task.Name}] DNS容灾切换到A记录失败: {ipAddress}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{TaskName}] 切换到A记录时发生异常", _task.Name);
            await _telegramService.SendErrorNotificationAsync($"[{_task.Name}] DNS容灾切换A记录异常: {ex.Message}");
        }
    }

    private async Task SwitchToCnameAsync()
    {
        try
        {
            if (_currentState == DnsRecordState.CnameRecord)
            {
                _logger.LogDebug("[{TaskName}] 已经是CNAME状态，跳过切换", _task.Name);
                return;
            }

            _logger.LogWarning("[{TaskName}] ⚠️ 触发容灾切换: 切换到CNAME备用域名 {BackupDomain}", _task.Name, _task.BackupDomain);

            var success = await _cloudflareService.SwitchToCnameAsync(_task.BackupDomain);

            if (success)
            {
                _currentState = DnsRecordState.CnameRecord;
                _hasCheckedDnsConsistency = false;
                await _telegramService.SendNotificationAsync(
                    $"⚠️ DNS容灾已触发\n\n" +
                    $"任务: {_task.Name}\n" +
                    $"域名: {_task.PrimaryDomain}\n" +
                    $"类型: CNAME\n" +
                    $"目标: {_task.BackupDomain}\n" +
                    $"原因: IP {_task.Ip}:{_task.PrimaryPort} 连续{_task.FailureThreshold}次不可达");
                _logger.LogInformation("[{TaskName}] 成功切换到CNAME: {BackupDomain}", _task.Name, _task.BackupDomain);
            }
            else
            {
                _logger.LogError("[{TaskName}] 切换到CNAME失败", _task.Name);
                await _telegramService.SendErrorNotificationAsync($"[{_task.Name}] DNS容灾切换到备用域名失败");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{TaskName}] 切换到CNAME时发生异常", _task.Name);
            await _telegramService.SendErrorNotificationAsync($"[{_task.Name}] DNS容灾切换异常: {ex.Message}");
        }
    }
}
