using Microsoft.Extensions.Logging;
using DNSDisaster.Models;

namespace DNSDisaster.Services;

public enum DnsRecordState
{
    ARecord,
    CnameRecord,
    Unknown
}

public class DnsMonitoringService
{
    private readonly ITcpPingService _tcpPingService;
    private readonly ICloudflareService _cloudflareService;
    private readonly ITelegramNotificationService _telegramService;
    private readonly IDnsResolverService _dnsResolverService;
    private readonly IIpProviderService _ipProviderService;
    private readonly MonitorTask _task;
    private readonly ILogger<DnsMonitoringService> _logger;

    private int _consecutiveFailures = 0;
    private DnsRecordState _currentState = DnsRecordState.Unknown;
    private string? _currentMonitoredIp;
    private bool _hasCheckedDnsConsistency = false;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    // 错误通知节流：记录各类错误上次通知时间和静默期内的累计次数
    private readonly Dictionary<string, (DateTime LastNotified, int SuppressedCount)> _errorThrottle = new();
    private static readonly TimeSpan ErrorNotifyCooldown = TimeSpan.FromMinutes(30);

    // 切换API失败重试计数：连续失败超过阈值后停止重试
    private readonly Dictionary<string, int> _switchFailCount = new();
    private const int MaxSwitchRetries = 3;

    private bool IsSwitchRetryExceeded(string switchKey, string switchName)
    {
        var count = _switchFailCount.GetValueOrDefault(switchKey, 0);
        if (count >= MaxSwitchRetries)
        {
            _logger.LogDebug("[{TaskName}] {SwitchName} 已达最大重试次数({Max})，停止重试",
                _task.Name, switchName, MaxSwitchRetries);
            return true;
        }

        return false;
    }

    public DnsMonitoringService(
        ITcpPingService tcpPingService,
        ICloudflareService cloudflareService,
        ITelegramNotificationService telegramService,
        IDnsResolverService dnsResolverService,
        IIpProviderService ipProviderService,
        MonitorTask task,
        ILogger<DnsMonitoringService> logger)
    {
        _tcpPingService = tcpPingService;
        _cloudflareService = cloudflareService;
        _telegramService = telegramService;
        _dnsResolverService = dnsResolverService;
        _ipProviderService = ipProviderService;
        _task = task;
        _logger = logger;
    }

    public async Task StartMonitoringAsync()
    {
        _logger.LogInformation("[{TaskName}] 开始DNS监控服务...", _task.Name);
        await _telegramService.SendNotificationAsync($"🚀 DNS灾难恢复系统已启动\n任务: {_task.Name}\n域名: {_task.PrimaryDomain}");

        await MonitoringLoopAsync(_cancellationTokenSource.Token);
    }

    public void Stop()
    {
        _logger.LogInformation("[{TaskName}] 停止DNS监控服务...", _task.Name);
        _cancellationTokenSource.Cancel();
    }

    private async Task MonitoringLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await PerformDnsMonitoringAsync(cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(_task.CheckIntervalSeconds), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{TaskName}] 监控循环中发生异常", _task.Name);
                await SendThrottledErrorAsync("loop_exception", $"[{_task.Name}] 监控循环异常: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(_task.CheckIntervalSeconds), cancellationToken);
            }
        }
    }

    private async Task PerformDnsMonitoringAsync(CancellationToken cancellationToken)
    {
        var currentIp = await _ipProviderService.GetCurrentIpAsync();

        if (string.IsNullOrEmpty(currentIp))
        {
            _logger.LogWarning("[{TaskName}] 无法获取当前IP地址，等待下次检测", _task.Name);
            return;
        }

        if (currentIp != _currentMonitoredIp)
        {
            _logger.LogInformation("[{TaskName}] 检测到IP变化: {OldIp} → {NewIp}", _task.Name, _currentMonitoredIp ?? "无", currentIp);
            _currentMonitoredIp = currentIp;
            _consecutiveFailures = 0;
            _hasCheckedDnsConsistency = false;
            // IP 变化说明环境已恢复，清除旧错误节流状态
            _errorThrottle.Remove("switch_a_fail");
            _errorThrottle.Remove("switch_cname_fail");
            _switchFailCount.Remove("switch_a_fail");
            _switchFailCount.Remove("switch_cname_fail");
        }

        _logger.LogDebug("[{TaskName}] 检测IP连通性: {IpAddress}:{Port}", _task.Name, currentIp, _task.PrimaryPort);
        var isIpReachable = await _tcpPingService.PingAsync(currentIp, _task.PrimaryPort);

        if (isIpReachable)
        {
            _logger.LogDebug("IP {IpAddress} 可达", currentIp);

            if (_consecutiveFailures > 0)
            {
                _logger.LogInformation("连接恢复，重置失败计数");
                _consecutiveFailures = 0;
                _errorThrottle.Remove("switch_a_fail");
                _errorThrottle.Remove("switch_cname_fail");
                _switchFailCount.Remove("switch_a_fail");
                _switchFailCount.Remove("switch_cname_fail");
            }

            if (!_hasCheckedDnsConsistency)
            {
                _logger.LogInformation("[{Timestamp}] 首次检测DNS一致性: IP={IpAddress}",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), currentIp);

                var domainIp = await _dnsResolverService.GetARecordAsync(_task.PrimaryDomain);

                if (string.IsNullOrEmpty(domainIp))
                {
                    _logger.LogDebug("[{Timestamp}] 主域名 {Domain} 无法解析为IP（可能是CNAME），准备切换到A记录",
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), _task.PrimaryDomain);
                    await SwitchToARecordAsync(currentIp, "主域名无法解析为IP");
                }
                else if (domainIp != currentIp)
                {
                    _logger.LogDebug("[{Timestamp}] 主域名IP不一致: 域名={DomainIp}, 当前={CurrentIp}",
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), domainIp, currentIp);
                    await SwitchToARecordAsync(currentIp, $"IP不一致 ({domainIp} → {currentIp})");
                }
                else
                {
                    _logger.LogDebug("[{Timestamp}] 主域名IP一致: {IpAddress}，无需更新",
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), currentIp);
                    _currentState = DnsRecordState.ARecord;
                    _hasCheckedDnsConsistency = true;
                }
            }
        }
        else
        {
            _consecutiveFailures++;
            _logger.LogWarning("[{TaskName}] ❌ IP {IpAddress} 不可达 (失败 {FailureCount}/{Threshold})",
                _task.Name, currentIp, _consecutiveFailures, _task.FailureThreshold);

            if (_consecutiveFailures >= _task.FailureThreshold)
            {
                await SwitchToCnameAsync();
                _consecutiveFailures = 0;
            }
        }
    }

    private async Task SwitchToARecordAsync(string ipAddress, string reason)
    {
        if (IsSwitchRetryExceeded("switch_a_fail", "切换A记录"))
        {
            _hasCheckedDnsConsistency = true;
            return;
        }

        try
        {
            _logger.LogInformation("[{TaskName}] 准备切换到A记录: IP={IpAddress}, 原因={Reason}", _task.Name, ipAddress, reason);

            var success = await _cloudflareService.SwitchToARecordAsync(ipAddress);

            if (success)
            {
                _currentState = DnsRecordState.ARecord;
                _hasCheckedDnsConsistency = true;
                _errorThrottle.Remove("switch_a_fail");
                _switchFailCount.Remove("switch_a_fail");
                await _telegramService.SendNotificationAsync(
                    $"✅ DNS记录已更新\n\n" +
                    $"任务: {_task.Name}\n" +
                    $"域名: {_task.PrimaryDomain}\n" +
                    $"类型: A记录\n" +
                    $"IP: {ipAddress}\n" +
                    $"原因: {reason}");
                _logger.LogInformation("[{TaskName}] 成功切换到A记录: {IpAddress}", _task.Name, ipAddress);
            }
            else
            {
                // 切换失败：标记已检测，避免下一轮立即重试；依赖节流控制通知频率
                _hasCheckedDnsConsistency = true;
                _switchFailCount["switch_a_fail"] = _switchFailCount.GetValueOrDefault("switch_a_fail", 0) + 1;
                _logger.LogError("[{TaskName}] 切换到A记录失败", _task.Name);
                await SendThrottledErrorAsync("switch_a_fail", $"[{_task.Name}] 切换到A记录失败: {ipAddress}");
            }
        }
        catch (Exception ex)
        {
            _hasCheckedDnsConsistency = true;
            _switchFailCount["switch_a_fail"] = _switchFailCount.GetValueOrDefault("switch_a_fail", 0) + 1;
            _logger.LogError(ex, "[{TaskName}] 切换到A记录时发生异常", _task.Name);
            await SendThrottledErrorAsync("switch_a_fail", $"[{_task.Name}] 切换A记录异常: {ex.Message}");
        }
    }

    private async Task SwitchToCnameAsync()
    {
        if (IsSwitchRetryExceeded("switch_cname_fail", "切换CNAME"))
        {
            return;
        }

        try
        {
            if (_currentState == DnsRecordState.CnameRecord)
            {
                _logger.LogDebug("[{TaskName}] 已经是CNAME状态，跳过切换", _task.Name);
                return;
            }

            _logger.LogWarning("[{TaskName}] ⚠️ 触发故障转移: 切换到CNAME备用域名 {BackupDomain}", _task.Name, _task.BackupDomain);

            var success = await _cloudflareService.SwitchToCnameAsync(_task.BackupDomain);

            if (success)
            {
                _currentState = DnsRecordState.CnameRecord;
                _hasCheckedDnsConsistency = false;
                _errorThrottle.Remove("switch_cname_fail");
                _switchFailCount.Remove("switch_cname_fail");
                await _telegramService.SendNotificationAsync(
                    $"⚠️ 故障转移已触发\n\n" +
                    $"任务: {_task.Name}\n" +
                    $"域名: {_task.PrimaryDomain}\n" +
                    $"类型: CNAME\n" +
                    $"目标: {_task.BackupDomain}\n" +
                    $"原因: IP连续{_task.FailureThreshold}次不可达\n" +
                    $"状态: 系统将继续监控新IP");
                _logger.LogInformation("[{TaskName}] 成功切换到CNAME: {BackupDomain}", _task.Name, _task.BackupDomain);
            }
            else
            {
                // 切换失败：标记为已处于 CNAME 状态，避免下轮重复发起；等待冷却后重试
                _currentState = DnsRecordState.CnameRecord;
                _switchFailCount["switch_cname_fail"] = _switchFailCount.GetValueOrDefault("switch_cname_fail", 0) + 1;
                _logger.LogError("[{TaskName}] 切换到CNAME失败", _task.Name);
                await SendThrottledErrorAsync("switch_cname_fail", $"[{_task.Name}] 故障转移到备用域名失败，将在冷却后重试");
            }
        }
        catch (Exception ex)
        {
            _currentState = DnsRecordState.CnameRecord;
            _switchFailCount["switch_cname_fail"] = _switchFailCount.GetValueOrDefault("switch_cname_fail", 0) + 1;
            _logger.LogError(ex, "[{TaskName}] 切换到CNAME时发生异常", _task.Name);
            await SendThrottledErrorAsync("switch_cname_fail", $"[{_task.Name}] 故障转移异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 节流错误通知：同类错误在冷却窗口内只发送一次，冷却到期发送时附带静默期内累计次数。
    /// </summary>
    private async Task SendThrottledErrorAsync(string errorKey, string message)
    {
        var now = DateTime.Now;

        if (_errorThrottle.TryGetValue(errorKey, out var entry))
        {
            if (now - entry.LastNotified < ErrorNotifyCooldown)
            {
                // 仍在冷却期，只记录日志，累计次数
                _errorThrottle[errorKey] = (entry.LastNotified, entry.SuppressedCount + 1);
                _logger.LogWarning("[{TaskName}] 错误通知已节流 ({ErrorKey})，冷却剩余 {Remaining:F0}s",
                    _task.Name, errorKey, (ErrorNotifyCooldown - (now - entry.LastNotified)).TotalSeconds);
                return;
            }

            // 冷却期已过，附上静默期内累计次数
            if (entry.SuppressedCount > 0)
            {
                message += $"\n（冷却期内已静默 {entry.SuppressedCount} 次）";
            }
        }

        _errorThrottle[errorKey] = (now, 0);
        await _telegramService.SendErrorNotificationAsync(message);
    }
}
