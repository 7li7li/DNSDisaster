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
    private readonly DNSDisasterSettings _settings;
    private readonly ILogger<DnsMonitoringService> _logger;

    private int _consecutiveFailures = 0;
    private DnsRecordState _currentState = DnsRecordState.Unknown;
    private string? _currentMonitoredIp;
    private bool _hasCheckedDnsConsistency = false; // 标记是否已检测过DNS一致性
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public DnsMonitoringService(
        ITcpPingService tcpPingService,
        ICloudflareService cloudflareService,
        ITelegramNotificationService telegramService,
        IDnsResolverService dnsResolverService,
        IIpProviderService ipProviderService,
        DNSDisasterSettings settings,
        ILogger<DnsMonitoringService> logger)
    {
        _tcpPingService = tcpPingService;
        _cloudflareService = cloudflareService;
        _telegramService = telegramService;
        _dnsResolverService = dnsResolverService;
        _ipProviderService = ipProviderService;
        _settings = settings;
        _logger = logger;
    }

    public async Task StartMonitoringAsync()
    {
        _logger.LogInformation("开始DNS监控服务...");
        await _telegramService.SendNotificationAsync("🚀 DNS灾难恢复系统已启动");

        // 启动主监控循环
        await MonitoringLoopAsync(_cancellationTokenSource.Token);
    }

    public void Stop()
    {
        _logger.LogInformation("停止DNS监控服务...");
        _cancellationTokenSource.Cancel();
    }

    private async Task MonitoringLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // 步骤1: 通过API获取当前IP
                var currentIp = await _ipProviderService.GetCurrentIpAsync();
                
                if (string.IsNullOrEmpty(currentIp))
                {
                    _logger.LogWarning("无法获取当前IP地址，等待下次检测");
                    await Task.Delay(TimeSpan.FromSeconds(_settings.CheckIntervalSeconds), cancellationToken);
                    continue;
                }

                // 检测IP是否变化
                if (currentIp != _currentMonitoredIp)
                {
                    _logger.LogInformation("检测到IP变化: {OldIp} → {NewIp}", _currentMonitoredIp ?? "无", currentIp);
                    _currentMonitoredIp = currentIp;
                    _consecutiveFailures = 0; // 重置失败计数
                    _hasCheckedDnsConsistency = false; // IP变化后需要重新检测DNS一致性
                }

                // 步骤2: TCPing检测该IP是否联通
                _logger.LogDebug("检测IP连通性: {IpAddress}:{Port}", currentIp, _settings.PrimaryPort);
                var isIpReachable = await _tcpPingService.PingAsync(currentIp, _settings.PrimaryPort);

                if (isIpReachable)
                {
                    // 步骤3: IP可达，只在第一次检查主域名是否指向该IP
                    _logger.LogDebug("✅ IP {IpAddress} 可达", currentIp);
                    
                    // 重置失败计数
                    if (_consecutiveFailures > 0)
                    {
                        _logger.LogInformation("连接恢复，重置失败计数");
                        _consecutiveFailures = 0;
                    }

                    // 只在第一次或IP变化后检测DNS一致性
                    if (!_hasCheckedDnsConsistency)
                    {
                        _logger.LogInformation("[{Timestamp}] 首次检测DNS一致性: IP={IpAddress}", 
                            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), currentIp);
                        
                        // 解析主域名当前的IP
                        var domainIp = await _dnsResolverService.GetARecordAsync(_settings.PrimaryDomain);
                        
                        if (string.IsNullOrEmpty(domainIp))
                        {
                            _logger.LogDebug("[{Timestamp}] 主域名 {Domain} 无法解析为IP（可能是CNAME），准备切换到A记录", 
                                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), _settings.PrimaryDomain);
                            
                            // 尝试切换到A记录
                            await SwitchToARecordAsync(currentIp, "主域名无法解析为IP");
                        }
                        else if (domainIp != currentIp)
                        {
                            _logger.LogDebug("[{Timestamp}] 主域名IP不一致: 域名={DomainIp}, 当前={CurrentIp}", 
                                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), domainIp, currentIp);
                            
                            // IP不一致，更新A记录
                            await SwitchToARecordAsync(currentIp, $"IP不一致 ({domainIp} → {currentIp})");
                        }
                        else
                        {
                            _logger.LogDebug("[{Timestamp}] 主域名IP一致: {IpAddress}，无需更新", 
                                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), currentIp);
                            _currentState = DnsRecordState.ARecord;
                        }
                        
                        // 标记已检测过
                        _hasCheckedDnsConsistency = true;
                    }
                }
                else
                {
                    // IP不可达，增加失败计数
                    _consecutiveFailures++;
                    _logger.LogWarning("❌ IP {IpAddress} 不可达 (失败 {FailureCount}/{Threshold})", 
                        currentIp, _consecutiveFailures, _settings.FailureThreshold);

                    // 达到失败阈值，切换到CNAME
                    if (_consecutiveFailures >= _settings.FailureThreshold)
                    {
                        await SwitchToCnameAsync();
                        _consecutiveFailures = 0; // 重置计数，继续监控新IP
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(_settings.CheckIntervalSeconds), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "监控循环中发生异常");
                await _telegramService.SendErrorNotificationAsync($"监控循环异常: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(_settings.CheckIntervalSeconds), cancellationToken);
            }
        }
    }

    private async Task SwitchToARecordAsync(string ipAddress, string reason)
    {
        try
        {
            _logger.LogInformation("准备切换到A记录: IP={IpAddress}, 原因={Reason}", ipAddress, reason);
            
            var success = await _cloudflareService.SwitchToARecordAsync(ipAddress);
            
            if (success)
            {
                _currentState = DnsRecordState.ARecord;
                await _telegramService.SendNotificationAsync(
                    $"✅ DNS记录已更新\n\n" +
                    $"域名: {_settings.PrimaryDomain}\n" +
                    $"类型: A记录\n" +
                    $"IP: {ipAddress}\n" +
                    $"原因: {reason}");
                _logger.LogInformation("成功切换到A记录: {IpAddress}", ipAddress);
            }
            else
            {
                _logger.LogError("切换到A记录失败");
                await _telegramService.SendErrorNotificationAsync($"切换到A记录失败: {ipAddress}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换到A记录时发生异常");
            await _telegramService.SendErrorNotificationAsync($"切换A记录异常: {ex.Message}");
        }
    }

    private async Task SwitchToCnameAsync()
    {
        try
        {
            // 如果已经是CNAME状态，不重复切换
            if (_currentState == DnsRecordState.CnameRecord)
            {
                _logger.LogDebug("已经是CNAME状态，跳过切换");
                return;
            }

            _logger.LogWarning("⚠️ 触发故障转移: 切换到CNAME备用域名 {BackupDomain}", _settings.BackupDomain);
            
            var success = await _cloudflareService.SwitchToCnameAsync(_settings.BackupDomain);
            
            if (success)
            {
                _currentState = DnsRecordState.CnameRecord;
                _hasCheckedDnsConsistency = false; // 切换到CNAME后，下次IP可达时需要重新检测
                await _telegramService.SendNotificationAsync(
                    $"⚠️ 故障转移已触发\n\n" +
                    $"域名: {_settings.PrimaryDomain}\n" +
                    $"类型: CNAME\n" +
                    $"目标: {_settings.BackupDomain}\n" +
                    $"原因: IP连续{_settings.FailureThreshold}次不可达\n" +
                    $"状态: 系统将继续监控新IP");
                _logger.LogInformation("成功切换到CNAME: {BackupDomain}", _settings.BackupDomain);
            }
            else
            {
                _logger.LogError("切换到CNAME失败");
                await _telegramService.SendErrorNotificationAsync("故障转移到备用域名失败");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换到CNAME时发生异常");
            await _telegramService.SendErrorNotificationAsync($"故障转移异常: {ex.Message}");
        }
    }
}