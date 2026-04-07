# 配置指南

## 配置文件结构

系统使用 `appsettings.json` 配置文件，支持两种独立的监控任务�?

1. **DNS容灾监控任务** (`MonitorTasks`) - 监控IP变化并自动更新DNS记录
2. **套餐监控任务** (`SubscriptionMonitorTasks`) - 监控套餐到期、流量使用和余额状�?

两种任务完全独立，可以单独配置或同时使用�?

## 完整配置示例

```json
{
  "MonitorTasks": [
    {
      "Name": "DNS-Task1",
      "Enabled": true,
      "PrimaryDomain": "example.com",
      "PrimaryPort": 12345,
      "BackupDomain": "backup.example.com",
      "CheckIntervalSeconds": 30,
      "FailureThreshold": 3,
      "IpProvider": {
        "Username": "your_username",
        "Password": "your_password",
        "DeviceGroupId": 1,
        "ApiBaseUrl": "https://api.example.com/v1",
        "DirectIpApiUrl": ""
      }
    }
  ],
  "SubscriptionMonitorTasks": [
    {
      "Name": "Subscription-User1",
      "CheckIntervalHours": 6,
      "IpProvider": {
        "Username": "your_username",
        "Password": "your_password",
        "DeviceGroupId": 1,
        "ApiBaseUrl": "https://api.example.com/v1"
      }
    }
  ],
  "Cloudflare": {
    "ApiToken": "your_cloudflare_api_token",
    "ZoneId": "your_zone_id"
  },
  "Telegram": {
    "BotToken": "your_telegram_bot_token",
    "ChatId": "your_chat_id",
    "ApiBaseUrl": "https://api.telegram.org"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "DNSDisaster": "Debug"
    }
  }
}
```

## DNS容灾监控任务 (MonitorTasks)

### 配置参数

| 参数 | 类型 | 必填 | 说明 | 示例 |
|------|------|------|------|------|
| Name | string | �?| 任务名称 | "DNS-Task1" |
| PrimaryDomain | string | �?| 主域�?| "example.com" |
| PrimaryPort | int | �?| 检测端�?| 12345 |
| BackupDomain | string | �?| 备用域名 | "backup.example.com" |
| CheckIntervalSeconds | int | �?| 检测间隔（秒） | 30 |
| FailureThreshold | int | �?| 故障转移阈�?| 3 |
| IpProvider | object | �?| IP提供商配�?| 见下�?|

### 使用场景

- 需要自动更新DNS记录指向动态IP
- 需要在IP不可达时自动切换到备用域�?
- 需要持续监控服务可用�?

### 示例：只配置DNS监控

```json
{
  "MonitorTasks": [
    {
      "Name": "MyDNS",
      "PrimaryDomain": "example.com",
      "PrimaryPort": 12345,
      "BackupDomain": "backup.example.com",
      "CheckIntervalSeconds": 30,
      "FailureThreshold": 3,
      "IpProvider": {
        "Username": "user1",
        "Password": "pass1",
        "DeviceGroupId": 1,
        "ApiBaseUrl": "https://api.example.com/v1"
      }
    }
  ],
  "SubscriptionMonitorTasks": [],
  "Cloudflare": {
    "ApiToken": "your_token",
    "ZoneId": "your_zone_id"
  },
  "Telegram": {
    "BotToken": "your_bot_token",
    "ChatId": "your_chat_id"
  }
}
```

## 套餐监控任务 (SubscriptionMonitorTasks)

### 配置参数

| 参数 | 类型 | 必填 | 说明 | 示例 |
|------|------|------|------|------|
| Name | string | �?| 任务名称 | "Subscription-User1" |
| CheckIntervalHours | int | �?| 检查间隔（小时�?| 6 |
| ApiSettings | object | �?| API认证配置 | 见下�?|

### API认证配置 (ApiSettings)

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| Username | string | �?| API用户�?|
| Password | string | �?| API密码 |
| ApiBaseUrl | string | �?| API基础URL |

注意：套餐监控不需�?`DeviceGroupId` �?`DirectIpApiUrl` 配置�?

### 监控内容

- 套餐到期时间�?天内到期发送警告）
- 流量使用情况（超�?0%发送警告）
- 钱包余额（不足续费且套餐快到期或流量不足时发送警告）

### 通知格式

```
⚠️ 套餐状态警�?[Subscription-User1]
👤 用户: user123
📦 套餐: Premium Plan
📊 流量使用: 1600.00/2048.00 GB (78.1%)

�?套餐将在 5.2 天后到期�?026-04-06 10:30:00�?
💰 余额不足：当前余�?0.22 元，续费需�?229.00 �?

�?时间: 2026-04-01 11:30:00
```

### 使用场景

- 需要监控多个账户的套餐状�?
- 需要提前收到续费提�?
- 需要监控流量使用情�?

### 示例：只配置套餐监控

```json
{
  "MonitorTasks": [],
  "SubscriptionMonitorTasks": [
    {
      "Name": "User1-Subscription",
      "CheckIntervalHours": 6,
      "ApiSettings": {
        "Username": "user1",
        "Password": "pass1",
        "ApiBaseUrl": "https://api.example.com/v1"
      }
    },
    {
      "Name": "User2-Subscription",
      "CheckIntervalHours": 12,
      "ApiSettings": {
        "Username": "user2",
        "Password": "pass2",
        "ApiBaseUrl": "https://api.example.com/v1"
      }
    }
  ],
  "Cloudflare": {
    "ApiToken": "",
    "ZoneId": ""
  },
  "Telegram": {
    "BotToken": "your_bot_token",
    "ChatId": "your_chat_id"
  }
}
```

注意：只配置套餐监控时，Cloudflare配置可以留空�?

## IP提供商配�?(IpProvider)

### 配置参数

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| Username | string | �?| API用户�?|
| Password | string | �?| API密码 |
| DeviceGroupId | int | �?| 设备组ID |
| ApiBaseUrl | string | �?| API基础URL |
| DirectIpApiUrl | string | �?| 直接IP查询API（推荐） |

### 示例

```json
"IpProvider": {
  "Username": "your_username",
  "Password": "your_password",
  "DeviceGroupId": 1,
  "ApiBaseUrl": "https://api.example.com/v1",
  "DirectIpApiUrl": ""
}
```

## 全局配置

### Cloudflare配置

仅DNS监控任务需要，套餐监控可以留空�?

```json
"Cloudflare": {
  "ApiToken": "your_cloudflare_api_token",
  "ZoneId": "your_zone_id"
}
```

### Telegram配置

所有任务共享，用于发送通知�?

```json
"Telegram": {
  "BotToken": "your_telegram_bot_token",
  "ChatId": "your_chat_id",
  "ApiBaseUrl": "https://api.telegram.org"
}
```

国内部署建议使用代理API：`https://tg-api.xxx.com`

## 配置验证规则

1. 至少需要配置一种任务（DNS监控或套餐监控）
2. DNS监控任务需要有效的Cloudflare配置
3. 所有任务都需要有效的Telegram配置
4. 每个任务的Name必须唯一
5. IpProvider配置必须完整

## 常见配置场景

### 场景1：单一DNS监控

适用于只需要DNS容灾功能的用户�?

```json
{
  "MonitorTasks": [
    {
      "Name": "MyService",
      "PrimaryDomain": "service.example.com",
      "PrimaryPort": 443,
      "BackupDomain": "backup.example.com",
      "CheckIntervalSeconds": 30,
      "FailureThreshold": 3,
      "IpProvider": { /* ... */ }
    }
  ],
  "SubscriptionMonitorTasks": [],
  "Cloudflare": { /* 必填 */ },
  "Telegram": { /* 必填 */ }
}
```

### 场景2：单一套餐监控

适用于只需要监控套餐状态的用户�?

```json
{
  "MonitorTasks": [],
  "SubscriptionMonitorTasks": [
    {
      "Name": "MyAccount",
      "CheckIntervalHours": 6,
      "ApiSettings": {
        "Username": "your_username",
        "Password": "your_password",
        "ApiBaseUrl": "https://api.example.com/v1"
      }
    }
  ],
  "Cloudflare": {
    "ApiToken": "",
    "ZoneId": ""
  },
  "Telegram": { /* 必填 */ }
}
```

### 场景3：DNS监控 + 套餐监控

适用于需要完整功能的用户�?

```json
{
  "MonitorTasks": [
    {
      "Name": "DNS-Service",
      "PrimaryDomain": "service.example.com",
      "PrimaryPort": 443,
      "BackupDomain": "backup.example.com",
      "CheckIntervalSeconds": 30,
      "FailureThreshold": 3,
      "IpProvider": {
        "Username": "user1",
        "Password": "pass1",
        "DeviceGroupId": 1,
        "ApiBaseUrl": "https://api.example.com/v1"
      }
    }
  ],
  "SubscriptionMonitorTasks": [
    {
      "Name": "Subscription-User1",
      "CheckIntervalHours": 6,
      "ApiSettings": {
        "Username": "user1",
        "Password": "pass1",
        "ApiBaseUrl": "https://api.example.com/v1"
      }
    }
  ],
  "Cloudflare": { /* 必填 */ },
  "Telegram": { /* 必填 */ }
}
```

### 场景4：多账户套餐监控

适用于需要监控多个账户的用户�?

```json
{
  "MonitorTasks": [],
  "SubscriptionMonitorTasks": [
    {
      "Name": "Account-A",
      "CheckIntervalHours": 6,
      "ApiSettings": {
        "Username": "userA",
        "Password": "passA",
        "ApiBaseUrl": "https://api.example.com/v1"
      }
    },
    {
      "Name": "Account-B",
      "CheckIntervalHours": 6,
      "ApiSettings": {
        "Username": "userB",
        "Password": "passB",
        "ApiBaseUrl": "https://api.example.com/v1"
      }
    }
  ],
  "Cloudflare": {
    "ApiToken": "",
    "ZoneId": ""
  },
  "Telegram": { /* 必填 */ }
}
```

## 配置文件位置

- 开发环境：`DNSDisaster/appsettings.json`
- 生产环境：与可执行文件同目录�?`appsettings.json`

## 配置热重�?

配置文件支持热重载，但需要重启服务才能生效：

```bash
sudo systemctl restart dns-disaster
```

## 故障排查

### 配置验证失败

启动时会自动验证配置，如果有错误会在控制台和日志中显示详细信息�?

### 查看当前配置

```bash
cat appsettings.json | jq .
```

### 测试配置

```bash
# 手动运行测试
./DNSDisaster

# 查看启动日志
sudo journalctl -u dns-disaster -n 50
```

## 更多信息

- [README.md](README.md) - 系统总体文档
- [SUBSCRIPTION_MONITORING.md](DNSDisaster/SUBSCRIPTION_MONITORING.md) - 套餐监控详细说明
- [DEPLOY.md](DNSDisaster/DEPLOY.md) - 部署指南


