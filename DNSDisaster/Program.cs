using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DNSDisaster.Models;
using DNSDisaster.Services;

namespace DNSDisaster;

class Program
{
    static async Task Main(string[] args)
    {
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
            Console.WriteLine("配置验证失败，请检查 appsettings.json 文件");
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
            return;
        }

        // 配置服务
        var services = new ServiceCollection();
        ConfigureServices(services, appSettings, configuration);

        // 构建服务提供者
        var serviceProvider = services.BuildServiceProvider();

        // 获取日志记录器
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("DNS灾难恢复系统启动中...");

        try
        {
            // 启动监控服务
            var monitoringService = serviceProvider.GetRequiredService<DnsMonitoringService>();
            
            // 处理Ctrl+C优雅退出
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                logger.LogInformation("收到退出信号，正在停止服务...");
                monitoringService.Stop();
            };

            await monitoringService.StartMonitoringAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "应用程序启动失败");
            Console.WriteLine($"启动失败: {ex.Message}");
        }
        finally
        {
            serviceProvider.Dispose();
        }

        Console.WriteLine("按任意键退出...");
        Console.ReadKey();
    }

    private static void ConfigureServices(IServiceCollection services, AppSettings appSettings, IConfiguration configuration)
    {
        // 添加日志
        services.AddLogging(builder =>
        {
            builder.AddConfiguration(configuration.GetSection("Logging"));
            builder.AddConsole();
        });

        // 添加HttpClient
        services.AddHttpClient();

        // 注册配置
        services.AddSingleton(appSettings.DNSDisaster);
        services.AddSingleton(appSettings.Cloudflare);
        services.AddSingleton(appSettings.Telegram);
        services.AddSingleton(appSettings.IpProvider);

        // 注册服务
        services.AddSingleton<ITcpPingService, TcpPingService>();
        services.AddSingleton<ICloudflareService, CloudflareDnsService>();
        services.AddSingleton<ITelegramNotificationService, TelegramNotificationService>();
        services.AddSingleton<IDnsResolverService, DnsResolverService>();
        services.AddSingleton<IIpProviderService, NyaTrpIpProviderService>();
        services.AddSingleton<DnsMonitoringService>();
    }

    private static bool ValidateConfiguration(AppSettings settings)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(settings.DNSDisaster.PrimaryDomain))
            errors.Add("PrimaryDomain 不能为空");

        if (settings.DNSDisaster.PrimaryPort <= 0)
            errors.Add("PrimaryPort 必须大于0");

        if (string.IsNullOrEmpty(settings.DNSDisaster.BackupDomain))
            errors.Add("BackupDomain 不能为空");

        if (string.IsNullOrEmpty(settings.Cloudflare.ApiToken))
            errors.Add("Cloudflare ApiToken 不能为空");

        if (string.IsNullOrEmpty(settings.Cloudflare.ZoneId))
            errors.Add("Cloudflare ZoneId 不能为空");

        if (string.IsNullOrEmpty(settings.Telegram.BotToken))
            errors.Add("Telegram BotToken 不能为空");

        if (string.IsNullOrEmpty(settings.Telegram.ChatId))
            errors.Add("Telegram ChatId 不能为空");

        if (string.IsNullOrEmpty(settings.IpProvider.Username))
            errors.Add("IpProvider Username 不能为空");

        if (string.IsNullOrEmpty(settings.IpProvider.Password))
            errors.Add("IpProvider Password 不能为空");

        if (settings.IpProvider.DeviceGroupId <= 0)
            errors.Add("IpProvider DeviceGroupId 必须大于0");

        if (string.IsNullOrEmpty(settings.IpProvider.ApiBaseUrl))
            errors.Add("IpProvider ApiBaseUrl 不能为空");

        if (!Uri.TryCreate(settings.IpProvider.ApiBaseUrl, UriKind.Absolute, out _))
            errors.Add("IpProvider ApiBaseUrl 必须是有效的URL");

        if (errors.Any())
        {
            Console.WriteLine("配置错误:");
            foreach (var error in errors)
            {
                Console.WriteLine($"- {error}");
            }
            return false;
        }

        return true;
    }
}