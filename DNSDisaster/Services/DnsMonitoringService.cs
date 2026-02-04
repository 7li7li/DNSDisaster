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
    private DnsRecordState _currentState = DnsRecordState.ARecord;
    private string? _lastKnownIp;
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
        await _telegramService.SendNotificationAsync("DNS灾难恢复系统已启动");

        // 获取初始IP地址
        _lastKnownIp = await _ipProviderService.GetCurrentIpAsync();
        if (string.IsNullOrEmpty(_lastKnownIp))
        {
            _logger.LogError("无法获取初始IP地址");
            await _telegramService.SendErrorNotificationAsync("无法获取初始IP地址，请检查IP提供商配置");
            return;
        }

        _logger.LogInformation("获取到初始IP地址: {IpAddress}", _lastKnownIp);

        // 启动主监控循环
        var monitoringTask = MonitoringLoopAsync(_cancellationTokenSource.Token);
        
        // 启动恢复检测循环
        var recoveryTask = RecoveryCheckLoopAsync(_cancellationTokenSource.Token);

        await Task.WhenAny(monitoringTask, recoveryTask);
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
                bool isConnected;
                string targetDescription;

                if (_currentState == DnsRecordState.ARecord && !string.IsNullOrEmpty(_lastKnownIp))
                {
                    // A记录状态：直接检测IP地址
                    isConnected = await _tcpPingService.PingAsync(_lastKnownIp, _settings.PrimaryPort);
                    targetDescription = $"{_lastKnownIp}:{_settings.PrimaryPort}";
                }
                else
                {
                    // CNAME状态或无IP：检测主域名
                    isConnected = await _tcpPingService.PingAsync(_settings.PrimaryDomain, _settings.PrimaryPort);
                    targetDescription = $"{_settings.PrimaryDomain}:{_settings.PrimaryPort}";
                }

                if (isConnected)
                {
                    // 连接成功，重置失败计数
                    if (_consecutiveFailures > 0)
                    {
                        _logger.LogInformation("连接恢复，重置失败计数 - {Target}", targetDescription);
                        _consecutiveFailures = 0;
                    }
                    else
                    {
                        _logger.LogDebug("TCP连接正常 - {Target}", targetDescription);
                    }
                }
                else
                {
                    // 连接失败，增加失败计数
                    _consecutiveFailures++;
                    _logger.LogWarning("连接失败 #{FailureCount}/{Threshold} - {Target}", 
                        _consecutiveFailures, _settings.FailureThreshold, targetDescription);

                    // 检查是否达到故障转移阈值
                    if (_consecutiveFailures >= _settings.FailureThreshold && _currentState == DnsRecordState.ARecord)
                    {
                        await TriggerFailoverAsync();
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

    private async Task RecoveryCheckLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // 只有在CNAME状态时才检查恢复
                if (_currentState == DnsRecordState.CnameRecord)
                {
                    await CheckForRecoveryAsync();
                }

                await Task.Delay(TimeSpan.FromSeconds(_settings.RecoveryCheckIntervalSeconds), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "恢复检测循环中发生异常");
                await Task.Delay(TimeSpan.FromSeconds(_settings.RecoveryCheckIntervalSeconds), cancellationToken);
            }
        }
    }

    private async Task TriggerFailoverAsync()
    {
        try
        {
            _logger.LogWarning("触发故障转移: 开始检查新IP可用性");
            
            // 首先尝试获取最新IP
            var currentIp = await _ipProviderService.GetCurrentIpAsync();
            
            if (!string.IsNullOrEmpty(currentIp))
            {
                _logger.LogInformation("获取到最新IP: {IpAddress}，测试连通性", currentIp);
                
                // 检查IP是否发生变化
                if (currentIp != _lastKnownIp)
                {
                    _logger.LogInformation("检测到IP变化: {OldIp} → {NewIp}", _lastKnownIp, currentIp);
                    _lastKnownIp = currentIp;
                    await _telegramService.SendNotificationAsync($"故障转移时检测到IP变化: {_lastKnownIp} → {currentIp}");
                }
                
                // 测试新IP是否可用
                var isNewIpAvailable = await _tcpPingService.PingAsync(currentIp, _settings.PrimaryPort);
                
                if (isNewIpAvailable)
                {
                    _logger.LogInformation("新IP {IpAddress} 可用，直接切换到新A记录", currentIp);
                    
                    var success = await _cloudflareService.SwitchToARecordAsync(currentIp);
                    
                    if (success)
                    {
                        _currentState = DnsRecordState.ARecord;
                        _consecutiveFailures = 0;
                        await _telegramService.SendNotificationAsync($"🔄 智能故障转移\n\n域名: {_settings.PrimaryDomain}\n新IP: {currentIp}\n状态: 已直接切换到新A记录\n原因: 检测到新IP可用");
                        _logger.LogInformation("智能故障转移成功: 直接切换到新A记录 {IpAddress}", currentIp);
                        return;
                    }
                    else
                    {
                        _logger.LogError("切换到新A记录失败，将尝试CNAME故障转移");
                    }
                }
                else
                {
                    _logger.LogWarning("新IP {IpAddress} 不可用，将切换到CNAME备用域名", currentIp);
                }
            }
            else
            {
                _logger.LogWarning("无法获取最新IP，将切换到CNAME备用域名");
            }
            
            // 如果新IP不可用或获取失败，则切换到CNAME备用域名
            _logger.LogWarning("执行CNAME故障转移: 切换到备用域名 {BackupDomain}", _settings.BackupDomain);
            
            var cnameSuccess = await _cloudflareService.SwitchToCnameAsync(_settings.BackupDomain);
            
            if (cnameSuccess)
            {
                _currentState = DnsRecordState.CnameRecord;
                await _telegramService.SendFailoverNotificationAsync(_settings.PrimaryDomain, _settings.BackupDomain);
                _logger.LogInformation("CNAME故障转移成功完成");
            }
            else
            {
                _logger.LogError("CNAME故障转移失败");
                await _telegramService.SendErrorNotificationAsync("故障转移到备用域名失败");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行故障转移时发生异常");
            await _telegramService.SendErrorNotificationAsync($"故障转移异常: {ex.Message}");
        }
    }

    private async Task CheckForRecoveryAsync()
    {
        try
        {
            // 获取最新的IP地址
            var currentIp = await _ipProviderService.GetCurrentIpAsync();
            
            if (string.IsNullOrEmpty(currentIp))
            {
                _logger.LogWarning("无法获取当前IP地址，跳过恢复检测");
                return;
            }

            // 检查IP是否发生变化
            if (currentIp != _lastKnownIp)
            {
                _logger.LogInformation("恢复检测时发现IP变化: {OldIp} → {NewIp}", _lastKnownIp, currentIp);
                _lastKnownIp = currentIp;
                await _telegramService.SendNotificationAsync($"📍 IP地址变化\n\n旧IP: {_lastKnownIp}\n新IP: {currentIp}\n状态: 正在测试新IP可用性");
            }

            // 测试新IP是否可用
            var isAvailable = await _tcpPingService.PingAsync(currentIp, _settings.PrimaryPort);
            
            if (isAvailable)
            {
                _logger.LogInformation("检测到服务恢复，准备切换回A记录，IP: {IpAddress}", currentIp);
                
                var success = await _cloudflareService.SwitchToARecordAsync(currentIp);
                
                if (success)
                {
                    _currentState = DnsRecordState.ARecord;
                    _consecutiveFailures = 0;
                    await _telegramService.SendRecoveryNotificationAsync(_settings.PrimaryDomain);
                    _logger.LogInformation("成功恢复到A记录，IP: {IpAddress}", currentIp);
                }
                else
                {
                    _logger.LogError("切换回A记录失败");
                    await _telegramService.SendErrorNotificationAsync("切换回A记录失败");
                }
            }
            else
            {
                _logger.LogDebug("当前IP {IpAddress} 仍不可用，继续等待", currentIp);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查恢复时发生异常");
        }
    }
}