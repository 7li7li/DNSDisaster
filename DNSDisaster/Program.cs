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
                fileSizeLimitBytes: 10485760, // 10MB
                rollOnFileSizeLimit: true)
            .CreateLogger();

        try
        {
            Log.Information("DNS灾难恢复系统启动中...");

            // 构建配置
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // 绑定配置
            var appSettings = new AppSettings();
            configuration.Bind(appSettings);

            // 验证配置
            if (!ValidateConfiguration(appSettings))
            {
                Log.Error("配置验证失败，请检查 appsettings.json 文件");
                Console.WriteLine("配置验证失败，请检查 appsettings.json 文件");
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
                return;
            }

            Log.Information("发现 {Count} 个监控任务", appSettings.MonitorTasks.Count);

            // 为每个任务创建监控服务
            var tasks = new List<Task>();
            
            foreach (var task in appSettings.MonitorTasks)
            {
                Log.Information("初始化任务: {TaskName} - {Domain}", task.Name, task.PrimaryDomain);
                
                // 为每个任务创建独立的服务提供者
                var services = new ServiceCollection();
                ConfigureServices(services, appSettings, task);
                var serviceProvider = services.BuildServiceProvider();
                
                var monitoringService = serviceProvider.GetRequiredService<DnsMonitoringService>();
                _monitoringServices.Add(monitoringService);
                
                // 启动监控任务
                tasks.Add(Task.Run(async () => await monitoringService.StartMonitoringAsync()));
            }
            
            // 处理Ctrl+C优雅退出
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                Log.Information("收到退出信号，正在停止所有服务...");
                foreach (var service in _monitoringServices)
                {
                    service.Stop();
                }
            };

            // 等待所有任务完成
            await Task.WhenAll(tasks);
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

    private static void ConfigureServices(IServiceCollection services, AppSettings appSettings, MonitorTask task)
    {
        // 添加Serilog日志
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: false); // 不要dispose，因为多个任务共享同一个logger
        });

        // 添加HttpClient
        services.AddHttpClient();

        // 注册全局配置
        services.AddSingleton(appSettings.Cloudflare);
        services.AddSingleton(appSettings.Telegram);
        
        // 注册任务特定配置
        services.AddSingleton(task);

        // 注册服务 - 为每个任务创建独立的IP提供商服务
        services.AddSingleton<ITcpPingService, TcpPingService>();
        services.AddSingleton<ICloudflareService>(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
            var cloudflareSettings = sp.GetRequiredService<CloudflareSettings>();
            var logger = sp.GetRequiredService<ILogger<CloudflareDnsService>>();
            return new CloudflareDnsService(httpClient, cloudflareSettings, task, logger);
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

    private static bool ValidateConfiguration(AppSettings settings)
    {
        var errors = new List<string>();

        if (settings.MonitorTasks == null || settings.MonitorTasks.Count == 0)
        {
            errors.Add("至少需要配置一个监控任务");
        }
        else
        {
            for (int i = 0; i < settings.MonitorTasks.Count; i++)
            {
                var task = settings.MonitorTasks[i];
                var prefix = $"任务 #{i + 1} ({task.Name})";

                if (string.IsNullOrEmpty(task.Name))
                    errors.Add($"{prefix}: Name 不能为空");

                if (string.IsNullOrEmpty(task.PrimaryDomain))
                    errors.Add($"{prefix}: PrimaryDomain 不能为空");

                if (task.PrimaryPort <= 0)
                    errors.Add($"{prefix}: PrimaryPort 必须大于0");

                if (string.IsNullOrEmpty(task.BackupDomain))
                    errors.Add($"{prefix}: BackupDomain 不能为空");

                if (task.IpProvider == null)
                {
                    errors.Add($"{prefix}: IpProvider 配置不能为空");
                }
                else
                {
                    if (string.IsNullOrEmpty(task.IpProvider.Username))
                        errors.Add($"{prefix}: IpProvider.Username 不能为空");

                    if (string.IsNullOrEmpty(task.IpProvider.Password))
                        errors.Add($"{prefix}: IpProvider.Password 不能为空");

                    if (task.IpProvider.DeviceGroupId <= 0)
                        errors.Add($"{prefix}: IpProvider.DeviceGroupId 必须大于0");

                    if (string.IsNullOrEmpty(task.IpProvider.ApiBaseUrl))
                        errors.Add($"{prefix}: IpProvider.ApiBaseUrl 不能为空");

                    if (!Uri.TryCreate(task.IpProvider.ApiBaseUrl, UriKind.Absolute, out _))
                        errors.Add($"{prefix}: IpProvider.ApiBaseUrl 必须是有效的URL");
                }
            }
        }

        if (string.IsNullOrEmpty(settings.Cloudflare.ApiToken))
            errors.Add("Cloudflare ApiToken 不能为空");

        if (string.IsNullOrEmpty(settings.Cloudflare.ZoneId))
            errors.Add("Cloudflare ZoneId 不能为空");

        if (string.IsNullOrEmpty(settings.Telegram.BotToken))
            errors.Add("Telegram BotToken 不能为空");

        if (string.IsNullOrEmpty(settings.Telegram.ChatId))
            errors.Add("Telegram ChatId 不能为空");

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
}
