# DNS灾难恢复系统

自动化DNS故障转移系统，通过持续监控动态IP的可用性，自动更新DNS记录，确保域名始终指向可用的服务器。

## 功能特性

- **动态IP监控** - 通过API实时获取最新IP地址
- **智能TCPing检测** - 持续检测IP端口连通性
- **自动DNS更新** - IP可达时自动检查并更新A记录
- **故障转移** - IP不可达时自动切换到CNAME备用域名
- **持续恢复检测** - 即使在CNAME状态也持续监控新IP
- **Telegram通知** - 实时通知DNS变更和系统状态
- **文件日志** - 自动记录日志到文件，支持日志滚动
- **Cloudflare集成** - 通过API自动管理DNS记录

## 快速开始

### 1. 下载程序

从发布页面下载 `DNSDisaster` 可执行文件。

### 2. 配置

创建 `appsettings.json`：

```json
{
  "MonitorTasks": [
    {
      "Name": "Task1",
      "PrimaryDomain": "your-domain.com",
      "PrimaryPort": 12345,
      "BackupDomain": "backup-domain.com",
      "CheckIntervalSeconds": 30,
      "FailureThreshold": 3,
      "IpProvider": {
        "Username": "your_username",
        "Password": "your_password",
        "DeviceGroupId": 1,
        "ApiBaseUrl": "https://nya.trp.sh/api/v1",
        "DirectIpApiUrl": "https://your-api.com/status"
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
    "ApiBaseUrl": "https://tg-api.xxx.com"
  }
}
```

**支持多任务**: 可以在 `MonitorTasks` 数组中配置多个监控任务，每个任务独立运行，拥有自己的域名和IP提供商配置。

### 3. 运行

```bash
chmod +x DNSDisaster
./DNSDisaster
```

## 工作原理

系统采用统一的监控循环，持续执行以下步骤：

1. **获取当前IP** - 通过API获取最新IP地址
2. **TCPing检测** - 测试IP端口是否可达
3. **IP可达时** - 检查主域名IP是否一致，不一致则更新A记录
4. **IP不可达时** - 累计失败次数，达到阈值后切换到CNAME备用域名
5. **持续监控** - 无论当前状态，都持续监控新IP

### 监控周期

- 每个检测周期（默认30秒）执行一次完整流程
- 持续运行，自动适应IP变化

## 配置说明

### 多任务配置

系统支持同时监控多个域名，每个任务独立运行：

```json
{
  "MonitorTasks": [
    {
      "Name": "Task1",
      "PrimaryDomain": "domain1.com",
      "PrimaryPort": 12345,
      "BackupDomain": "backup1.com",
      "CheckIntervalSeconds": 30,
      "FailureThreshold": 3,
      "IpProvider": {
        "Username": "user1",
        "Password": "pass1",
        "DeviceGroupId": 1,
        "ApiBaseUrl": "https://nya.trp.sh/api/v1",
        "DirectIpApiUrl": ""
      }
    },
    {
      "Name": "Task2",
      "PrimaryDomain": "domain2.com",
      "PrimaryPort": 23456,
      "BackupDomain": "backup2.com",
      "CheckIntervalSeconds": 30,
      "FailureThreshold": 3,
      "IpProvider": {
        "Username": "user2",
        "Password": "pass2",
        "DeviceGroupId": 2,
        "ApiBaseUrl": "https://nya.trp.sh/api/v1",
        "DirectIpApiUrl": "https://api2.com/status"
      }
    }
  ],
  "Cloudflare": {
    "ApiToken": "shared_token",
    "ZoneId": "shared_zone_id"
  },
  "Telegram": {
    "BotToken": "shared_bot_token",
    "ChatId": "shared_chat_id",
    "ApiBaseUrl": "https://tg-api.xxx.com"
  }
}
```

### 任务配置 (MonitorTask)

| 配置项 | 说明 | 示例 |
|--------|------|------|
| Name | 任务名称（用于日志标识） | Task1 |
| PrimaryDomain | 要监控的主域名 | example.com |
| PrimaryPort | TCPing检测的目标端口 | 12345 |
| BackupDomain | 故障时切换的备用域名 | backup.example.com |
| CheckIntervalSeconds | 检测间隔（秒） | 30 |
| FailureThreshold | 触发故障转移的失败次数 | 3 |
| IpProvider | IP提供商配置（每个任务独立） | 见下文 |

### 全局配置

**Cloudflare** 和 **Telegram** 配置在所有任务间共享。

### Cloudflare 配置

获取方式：
1. 登录 [Cloudflare Dashboard](https://dash.cloudflare.com/)
2. 选择域名，复制 Zone ID
3. 创建API令牌：My Profile > API Tokens
4. 权限：Zone:DNS:Edit

### Telegram 配置

获取方式：
1. 与 [@BotFather](https://t.me/botfather) 创建机器人
2. 与 [@userinfobot](https://t.me/userinfobot) 获取 Chat ID
3. 大陆部署使用 `https://tg-api.xxx.com`

### IP Provider 配置

每个任务可以有自己的IP提供商配置，支持两种方式：

**方式1: 直接IP查询API（推荐）**
```json
"IpProvider": {
  "DirectIpApiUrl": "https://your-api.com/status",
  "Username": "",
  "Password": "",
  "DeviceGroupId": 1,
  "ApiBaseUrl": "https://nya.trp.sh/api/v1"
}
```
API响应格式：`{"current_ip": "1.2.3.4"}`

**方式2: nya.trp.sh 设备组API**
```json
"IpProvider": {
  "Username": "your_username",
  "Password": "your_password",
  "DeviceGroupId": 1,
  "ApiBaseUrl": "https://nya.trp.sh/api/v1",
  "DirectIpApiUrl": ""
}
```

系统会优先使用直接IP查询API，失败时自动回退到设备组API。

**注意**: 不同任务可以使用不同的IP提供商配置，实现灵活的多源监控。

## 日志管理

### 日志文件

- 位置：`logs/dns-disaster-YYYYMMDD.log`
- 滚动：每天创建新文件
- 保留：30天
- 大小：单文件最大10MB

### 查看日志

```bash
# 实时查看
tail -f logs/dns-disaster-$(date +%Y%m%d).log

# 搜索错误
grep "ERR" logs/dns-disaster-*.log

# 查看最近100行
tail -n 100 logs/dns-disaster-$(date +%Y%m%d).log
```

详细说明见 [LOGGING.md](DNSDisaster/LOGGING.md)

## 通知消息

系统会发送以下类型的Telegram通知：

- 🚀 系统启动通知
- ✅ DNS记录更新通知（A记录变更）
- ⚠️ 故障转移通知（切换到CNAME）
- 📍 IP地址变化通知
- ❌ 错误和异常通知

## 部署

### Linux服务器

详细部署步骤见 [DEPLOY.md](DNSDisaster/DEPLOY.md)

快速部署：
```bash
# 上传文件
scp DNSDisaster user@server:/opt/dns-disaster/
scp appsettings.json user@server:/opt/dns-disaster/

# 设置权限
chmod +x /opt/dns-disaster/DNSDisaster

# 创建systemd服务
sudo nano /etc/systemd/system/dns-disaster.service

# 启动服务
sudo systemctl enable dns-disaster
sudo systemctl start dns-disaster
```

### Docker（可选）

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY DNSDisaster .
COPY appsettings.json .
RUN chmod +x DNSDisaster
CMD ["./DNSDisaster"]
```

## 系统要求

- Linux操作系统（x64）
- 网络连接（访问Cloudflare、Telegram、IP提供商API）
- 无需安装.NET运行时（自包含）
- 无需安装ICU库（已禁用全球化支持）

## 性能优化

- **直接IP查询API** - 相比设备组API，性能提升约7.5倍
- **统一监控循环** - 减少独立线程，降低资源消耗
- **智能状态管理** - 避免重复的DNS切换操作

## 注意事项

1. **Cloudflare配置**
   - 确保API令牌有足够权限（Zone:DNS:Edit）
   - 建议设置较短的TTL（300秒）以加快DNS传播

2. **备用域名**
   - 备用域名应该指向可用的服务
   - 系统不会检测备用域名的可用性

3. **IP提供商**
   - 优先配置直接IP查询API以获得更好性能
   - 确保API返回格式正确（包含 `current_ip` 字段）

4. **监控策略**
   - 系统会持续监控，即使在CNAME状态也会检测新IP
   - 检测间隔不宜过短，避免API限流

## 故障排查

### 常见问题

**服务无法启动**
```bash
# 查看错误日志
sudo journalctl -u dns-disaster -n 50

# 手动运行测试
./DNSDisaster
```

**Telegram通知不工作**
```bash
# 测试API连接
curl https://tg-api.xxx.com/botYOUR_BOT_TOKEN/getMe
```

**DNS更新失败**
```bash
# 测试Cloudflare API
curl -X GET "https://api.cloudflare.com/client/v4/zones/YOUR_ZONE_ID" \
  -H "Authorization: Bearer YOUR_API_TOKEN"
```

更多故障排查见 [DEPLOY.md](DNSDisaster/DEPLOY.md)

## 文档

- [DEPLOY.md](DNSDisaster/DEPLOY.md) - 部署指南
- [LOGGING.md](DNSDisaster/LOGGING.md) - 日志管理
- [appsettings.example.json](DNSDisaster/appsettings.example.json) - 配置示例

## 许可证

MIT License
