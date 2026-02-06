namespace DNSDisaster.Models;

public class AppSettings
{
    public DNSDisasterSettings DNSDisaster { get; set; } = new();
    public CloudflareSettings Cloudflare { get; set; } = new();
    public TelegramSettings Telegram { get; set; } = new();
    public IpProviderSettings IpProvider { get; set; } = new();
}

public class DNSDisasterSettings
{
    public string PrimaryDomain { get; set; } = string.Empty;
    public int PrimaryPort { get; set; }
    public string BackupDomain { get; set; } = string.Empty;
    public int CheckIntervalSeconds { get; set; } = 30;
    public int FailureThreshold { get; set; } = 3;
}

public class CloudflareSettings
{
    public string ApiToken { get; set; } = string.Empty;
    public string ZoneId { get; set; } = string.Empty;
}

public class TelegramSettings
{
    public string BotToken { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = "https://tg-api.7li7li.com";
}

public class IpProviderSettings
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int DeviceGroupId { get; set; } = 235;
    public string ApiBaseUrl { get; set; } = "https://nya.trp.sh/api/v1";
    public string? DirectIpApiUrl { get; set; } = null;
}