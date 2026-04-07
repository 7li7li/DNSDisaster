using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using DNSDisaster.Models;
using DNSDisaster.Services;

namespace DNSDisaster;

class Program
{
    private static List<DnsMonitoringService> _monitoringServices = new();
    private static List<DnsFailoverMonitoringService> _dnsFailoverServices = new();
    private static List<ISubscriptionMonitorService> _subscriptionServices = new();

    static async Task Main(string[] args)
    {
        // 配置Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("System", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: "logs/dns-disaster-.log",
                rollingInterval: RollingInterval.Day,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 10485760,
                rollOnFileSizeLimit: true)
            .CreateLogger();

        try
        {
            Log.Information("DNS灾难恢复系统启动中...");

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var appSettings = new AppSettings();
            configuration.Bind(appSettings);

            if (!ValidateConfiguration(appSettings))
            {
                Log.Error("配置验证失败，请检查 appsettings.json 文件");
                Console.WriteLine("配置验证失败，请检查 appsettings.json 文件");
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
                return;
            }

            Log.Information("发现 {DnsCount} 个DNS监控任务，{FailoverCount} 个DNS容灾任务，{SubCount} 个套餐监控任务",
                appSettings.MonitorTasks.Count,
                appSettings.DnsFailoverTasks.Count,
                appSettings.SubscriptionMonitorTasks.Count);

            var tasks = new List<Task>();

            foreach (var task in appSettings.MonitorTasks)
            {
                if (!task.Enabled)
                {
                    Log.Information("跳过已禁用的DNS监控任务: {TaskName}", task.Name);
                    continue;
                }

                Log.Information("初始化DNS监控任务: {TaskName} - {Domain}", task.Name, task.PrimaryDomain);

                var services = new ServiceCollection();
                ConfigureDnsMonitoringServices(services, appSettings, task);
                var serviceProvider = services.BuildServiceProvider();

                var monitoringService = serviceProvider.GetRequiredService<DnsMonitoringService>();
                _monitoringServices.Add(monitoringService);
                tasks.Add(Task.Run(async () => await monitoringService.StartMonitoringAsync()));
            }

            foreach (var task in appSettings.DnsFailoverTasks)
            {
                if (!task.Enabled)
                {
                    Log.Information("跳过已禁用的DNS容灾任务: {TaskName}", task.Name);
                    continue;
                }

                Log.Information("初始化DNS容灾任务: {TaskName} - {Domain} -> {Ip}:{Port}",
                    task.Name, task.PrimaryDomain, task.Ip, task.PrimaryPort);

                var services = new ServiceCollection();
                ConfigureDnsFailoverServices(services, appSettings, task);
                var serviceProvider = services.BuildServiceProvider();

                var failoverService = serviceProvider.GetRequiredService<DnsFailoverMonitoringService>();
                _dnsFailoverServices.Add(failoverService);
                tasks.Add(Task.Run(async () => await failoverService.StartMonitoringAsync()));
            }

            foreach (var task in appSettings.SubscriptionMonitorTasks)
            {
                if (!task.Enabled)
                {
                    Log.Information("跳过已禁用的套餐监控任务: {TaskName}", task.Name);
                    continue;
                }

                Log.Information("初始化套餐监控任务: {TaskName}", task.Name);

                var services = new ServiceCollection();
                ConfigureSubscriptionMonitoringServices(services, appSettings, task);
                var serviceProvider = services.BuildServiceProvider();

                var subscriptionService = serviceProvider.GetRequiredService<ISubscriptionMonitorService>();
                _subscriptionServices.Add(subscriptionService);
                tasks.Add(Task.Run(async () => await subscriptionService.StartMonitoringAsync(task)));
            }

            if (tasks.Count == 0)
            {
                Log.Error("没有启动任何任务，请检查配置");
                Console.WriteLine("错误: 没有启动任何任务，请检查配置文件");
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
                return;
            }

            Log.Information("已启动 {Count} 个监控任务", tasks.Count);

            var cancellationTokenSource = new CancellationTokenSource();
            var exitRequested = false;

            Console.CancelKeyPress += (sender, e) =>
            {
                if (exitRequested)
                {
                    Log.Warning("强制退出程序");
                    Environment.Exit(0);
                }

                e.Cancel = true;
                exitRequested = true;
                Log.Information("收到退出信号，正在停止所有服务...");

                foreach (var service in _monitoringServices)
                {
                    try
                    {
                        service.Stop();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "停止DNS监控服务时发生错误");
                    }
                }

                foreach (var service in _dnsFailoverServices)
                {
                    try
                    {
                        service.Stop();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "停止DNS容灾服务时发生错误");
                    }
                }

                foreach (var service in _subscriptionServices)
                {
                    try
                    {
                        service.Stop();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "停止套餐监控服务时发生错误");
                    }
                }

                cancellationTokenSource.Cancel();

                Log.Information("提示: 再次按 Ctrl+C 可强制退出");
            };

            try
            {
                await Task.WhenAll(tasks).WaitAsync(cancellationTokenSource.Token);
                Log.Information("所有任务已正常完成");
            }
            catch (OperationCanceledException)
            {
                Log.Information("所有任务已取消");

                try
                {
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await Task.WhenAll(tasks).WaitAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException)
                {
                    Log.Information("任务清理完成");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "等待任务清理时发生错误");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "任务执行过程中发生错误");
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "应用程序启动失败");
            Console.WriteLine($"启动失败: {ex.Message}");
        }
        finally
        {
            Log.Information("DNS灾难恢复系统已停止");
            Log.CloseAndFlush();
        }

        Console.WriteLine("按任意键退出...");
        Console.ReadKey();
    }

    private static void ConfigureDnsMonitoringServices(IServiceCollection services, AppSettings appSettings, MonitorTask task)
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: false);
        });

        services.AddHttpClient();

        services.AddSingleton(appSettings.Cloudflare);
        services.AddSingleton(appSettings.Telegram);
        services.AddSingleton(task);

        services.AddSingleton<ITcpPingService, TcpPingService>();
        services.AddSingleton<ICloudflareService>(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
            var cloudflareSettings = sp.GetRequiredService<CloudflareSettings>();
            var logger = sp.GetRequiredService<ILogger<CloudflareDnsService>>();
            return new CloudflareDnsService(httpClient, cloudflareSettings, task.PrimaryDomain, logger);
        });
        services.AddSingleton<ITelegramNotificationService, TelegramNotificationService>();
        services.AddSingleton<IDnsResolverService, DnsResolverService>();
        services.AddSingleton<IIpProviderService>(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
            var logger = sp.GetRequiredService<ILogger<NyaTrpIpProviderService>>();
            return new NyaTrpIpProviderService(httpClient, task.IpProvider, logger);
        });
        services.AddSingleton<DnsMonitoringService>();
    }

    private static void ConfigureDnsFailoverServices(IServiceCollection services, AppSettings appSettings, DnsFailoverTask task)
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: false);
        });

        services.AddHttpClient();

        services.AddSingleton(appSettings.Cloudflare);
        services.AddSingleton(appSettings.Telegram);
        services.AddSingleton(task);

        services.AddSingleton<ITcpPingService, TcpPingService>();
        services.AddSingleton<ICloudflareService>(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
            var cloudflareSettings = sp.GetRequiredService<CloudflareSettings>();
            var logger = sp.GetRequiredService<ILogger<CloudflareDnsService>>();
            return new CloudflareDnsService(httpClient, cloudflareSettings, task.PrimaryDomain, logger);
        });
        services.AddSingleton<ITelegramNotificationService, TelegramNotificationService>();
        services.AddSingleton<IDnsResolverService, DnsResolverService>();
        services.AddSingleton<DnsFailoverMonitoringService>();
    }

    private static void ConfigureSubscriptionMonitoringServices(IServiceCollection services, AppSettings appSettings, SubscriptionMonitorTask task)
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: false);
        });

        services.AddHttpClient();

        services.AddSingleton(appSettings.Telegram);

        services.AddSingleton<ITelegramNotificationService, TelegramNotificationService>();
        services.AddSingleton<ISubscriptionMonitorService, SubscriptionMonitorService>();
    }

    private static bool ValidateConfiguration(AppSettings settings)
    {
        var errors = new List<string>();

        if (settings.MonitorTasks == null || settings.MonitorTasks.Count == 0)
        {
            Log.Warning("未配置DNS监控任务");
        }
        else
        {
            for (int i = 0; i < settings.MonitorTasks.Count; i++)
            {
                var task = settings.MonitorTasks[i];
                var prefix = $"DNS监控任务 #{i + 1} ({task.Name})";

                if (string.IsNullOrEmpty(task.Name))
                    errors.Add($"{prefix}: Name 不能为空");

                if (string.IsNullOrEmpty(task.PrimaryDomain))
                    errors.Add($"{prefix}: PrimaryDomain 不能为空");

                if (task.PrimaryPort <= 0)
                    errors.Add($"{prefix}: PrimaryPort 必须大于0");

                if (string.IsNullOrEmpty(task.BackupDomain))
                    errors.Add($"{prefix}: BackupDomain 不能为空");

                if (task.CheckIntervalSeconds <= 0)
                    errors.Add($"{prefix}: CheckIntervalSeconds 必须大于0");

                if (task.FailureThreshold <= 0)
                    errors.Add($"{prefix}: FailureThreshold 必须大于0");

                if (task.IpProvider == null)
                {
                    errors.Add($"{prefix}: IpProvider 配置不能为空");
                }
                else
                {
                    ValidateIpProviderSettings(task.IpProvider, prefix, errors);
                }
            }
        }

        if (settings.DnsFailoverTasks == null || settings.DnsFailoverTasks.Count == 0)
        {
            Log.Warning("未配置DNS容灾任务");
        }
        else
        {
            for (int i = 0; i < settings.DnsFailoverTasks.Count; i++)
            {
                var task = settings.DnsFailoverTasks[i];
                var prefix = $"DNS容灾任务 #{i + 1} ({task.Name})";

                if (string.IsNullOrEmpty(task.Name))
                    errors.Add($"{prefix}: Name 不能为空");

                if (string.IsNullOrEmpty(task.PrimaryDomain))
                    errors.Add($"{prefix}: PrimaryDomain 不能为空");

                if (string.IsNullOrEmpty(task.Ip))
                    errors.Add($"{prefix}: Ip 不能为空");
                else if (!IPAddress.TryParse(task.Ip, out _))
                    errors.Add($"{prefix}: Ip 必须是有效的IP地址");

                if (task.PrimaryPort <= 0)
                    errors.Add($"{prefix}: PrimaryPort 必须大于0");

                if (string.IsNullOrEmpty(task.BackupDomain))
                    errors.Add($"{prefix}: BackupDomain 不能为空");

                if (task.CheckIntervalSeconds <= 0)
                    errors.Add($"{prefix}: CheckIntervalSeconds 必须大于0");

                if (task.FailureThreshold <= 0)
                    errors.Add($"{prefix}: FailureThreshold 必须大于0");
            }
        }

        if (settings.SubscriptionMonitorTasks != null && settings.SubscriptionMonitorTasks.Count > 0)
        {
            for (int i = 0; i < settings.SubscriptionMonitorTasks.Count; i++)
            {
                var task = settings.SubscriptionMonitorTasks[i];
                var prefix = $"套餐监控任务 #{i + 1} ({task.Name})";

                if (string.IsNullOrEmpty(task.Name))
                    errors.Add($"{prefix}: Name 不能为空");

                if (task.CheckIntervalHours <= 0)
                    errors.Add($"{prefix}: CheckIntervalHours 必须大于0");

                if (task.ApiSettings == null)
                {
                    errors.Add($"{prefix}: ApiSettings 配置不能为空");
                }
                else
                {
                    ValidateSubscriptionApiSettings(task.ApiSettings, prefix, errors);
                }
            }
        }

        var hasDnsTasks = (settings.MonitorTasks?.Count ?? 0) > 0 || (settings.DnsFailoverTasks?.Count ?? 0) > 0;
        if (hasDnsTasks)
        {
            if (string.IsNullOrEmpty(settings.Cloudflare.ApiToken))
                errors.Add("Cloudflare ApiToken 不能为空");

            if (string.IsNullOrEmpty(settings.Cloudflare.ZoneId))
                errors.Add("Cloudflare ZoneId 不能为空");
        }

        if (string.IsNullOrEmpty(settings.Telegram.BotToken))
            errors.Add("Telegram BotToken 不能为空");

        if (string.IsNullOrEmpty(settings.Telegram.ChatId))
            errors.Add("Telegram ChatId 不能为空");

        if ((settings.MonitorTasks == null || settings.MonitorTasks.Count == 0) &&
            (settings.DnsFailoverTasks == null || settings.DnsFailoverTasks.Count == 0) &&
            (settings.SubscriptionMonitorTasks == null || settings.SubscriptionMonitorTasks.Count == 0))
        {
            errors.Add("至少需要配置一个DNS监控任务、DNS容灾任务或套餐监控任务");
        }
        else
        {
            var enabledDnsTasks = settings.MonitorTasks?.Count(t => t.Enabled) ?? 0;
            var enabledDnsFailoverTasks = settings.DnsFailoverTasks?.Count(t => t.Enabled) ?? 0;
            var enabledSubTasks = settings.SubscriptionMonitorTasks?.Count(t => t.Enabled) ?? 0;

            if (enabledDnsTasks == 0 && enabledDnsFailoverTasks == 0 && enabledSubTasks == 0)
            {
                errors.Add("至少需要启用一个DNS监控任务、DNS容灾任务或套餐监控任务（设置 Enabled: true）");
            }
            else
            {
                Log.Information("已启用 {DnsCount} 个DNS监控任务，{FailoverCount} 个DNS容灾任务，{SubCount} 个套餐监控任务",
                    enabledDnsTasks, enabledDnsFailoverTasks, enabledSubTasks);
            }
        }

        if (errors.Any())
        {
            Log.Error("配置错误:");
            foreach (var error in errors)
            {
                Log.Error("- {Error}", error);
                Console.WriteLine($"- {error}");
            }
            return false;
        }

        return true;
    }

    private static void ValidateIpProviderSettings(IpProviderSettings ipProvider, string prefix, List<string> errors)
    {
        if (string.IsNullOrEmpty(ipProvider.Username))
            errors.Add($"{prefix}: IpProvider.Username 不能为空");

        if (string.IsNullOrEmpty(ipProvider.Password))
            errors.Add($"{prefix}: IpProvider.Password 不能为空");

        if (ipProvider.DeviceGroupId <= 0)
            errors.Add($"{prefix}: IpProvider.DeviceGroupId 必须大于0");

        if (string.IsNullOrEmpty(ipProvider.ApiBaseUrl))
            errors.Add($"{prefix}: IpProvider.ApiBaseUrl 不能为空");

        if (!Uri.TryCreate(ipProvider.ApiBaseUrl, UriKind.Absolute, out _))
            errors.Add($"{prefix}: IpProvider.ApiBaseUrl 必须是有效的URL");
    }

    private static void ValidateSubscriptionApiSettings(SubscriptionApiSettings apiSettings, string prefix, List<string> errors)
    {
        if (string.IsNullOrEmpty(apiSettings.Username))
            errors.Add($"{prefix}: ApiSettings.Username 不能为空");

        if (string.IsNullOrEmpty(apiSettings.Password))
            errors.Add($"{prefix}: ApiSettings.Password 不能为空");

        if (string.IsNullOrEmpty(apiSettings.ApiBaseUrl))
            errors.Add($"{prefix}: ApiSettings.ApiBaseUrl 不能为空");

        if (!Uri.TryCreate(apiSettings.ApiBaseUrl, UriKind.Absolute, out _))
            errors.Add($"{prefix}: ApiSettings.ApiBaseUrl 必须是有效的URL");
    }
}
