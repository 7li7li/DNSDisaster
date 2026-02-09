# DNS灾难恢复系统 - 部署指南

## 快速开始

### 1. 上传文件

```bash
# 上传可执行文件和配置
scp DNSDisaster user@server:/opt/dns-disaster/
scp appsettings.json user@server:/opt/dns-disaster/
```

### 2. 配置并运行

```bash
# 设置权限
chmod +x /opt/dns-disaster/DNSDisaster

# 创建日志目录
mkdir -p /opt/dns-disaster/logs

# 编辑配置
nano /opt/dns-disaster/appsettings.json

# 测试运行
cd /opt/dns-disaster
./DNSDisaster
```

### 3. 设置为系统服务

```bash
# 创建服务文件
sudo nano /etc/systemd/system/dns-disaster.service
```

内容：
```ini
[Unit]
Description=DNS Disaster Recovery System
After=network.target

[Service]
Type=simple
User=root
WorkingDirectory=/opt/dns-disaster
ExecStart=/opt/dns-disaster/DNSDisaster
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

启动服务：
```bash
sudo systemctl daemon-reload
sudo systemctl enable dns-disaster
sudo systemctl start dns-disaster
sudo systemctl status dns-disaster
```

## 配置说明

### appsettings.json

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
        "ApiBaseUrl": "https://api.example.com/v1",
        "DirectIpApiUrl": "https://ip-api.example.com/status"
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

**支持多任务**: 可以在 `MonitorTasks` 数组中配置多个监控任务，每个任务独立运行。

### 多任务配置示例

```json
{
  "MonitorTasks": [
    {
      "Name": "Task1",
      "PrimaryDomain": "domain1.example.com",
      "PrimaryPort": 12345,
      "BackupDomain": "backup1.example.com",
      "CheckIntervalSeconds": 30,
      "FailureThreshold": 3,
      "IpProvider": {
        "Username": "user1",
        "Password": "pass1",
        "DeviceGroupId": 1,
        "ApiBaseUrl": "https://api.example.com/v1",
        "DirectIpApiUrl": ""
      }
    },
    {
      "Name": "Task2",
      "PrimaryDomain": "domain2.example.com",
      "PrimaryPort": 23456,
      "BackupDomain": "backup2.example.com",
      "CheckIntervalSeconds": 30,
      "FailureThreshold": 3,
      "IpProvider": {
        "Username": "user2",
        "Password": "pass2",
        "DeviceGroupId": 2,
        "ApiBaseUrl": "https://api.example.com/v1",
        "DirectIpApiUrl": "https://ip-api2.example.com/status"
      }
    }
  ],
  "Cloudflare": {
    "ApiToken": "shared_cloudflare_token",
    "ZoneId": "shared_zone_id"
  },
  "Telegram": {
    "BotToken": "shared_bot_token",
    "ChatId": "shared_chat_id",
    "ApiBaseUrl": "https://tg-api.xxx.com"
  }
}
```

**注意**: 
- 每个任务可以有独立的域名、端口和IP提供商配置
- Cloudflare和Telegram配置在所有任务间共享
- 所有任务并行运行，互不影响

### 获取配置信息

**Cloudflare**:
1. 登录 [Cloudflare Dashboard](https://dash.cloudflare.com/)
2. 选择域名，复制右侧的 Zone ID
3. 创建API令牌：My Profile > API Tokens > Create Token
4. 权限：Zone:DNS:Edit

**Telegram**:
1. 与 [@BotFather](https://t.me/botfather) 创建机器人，获取 Bot Token
2. 与 [@userinfobot](https://t.me/userinfobot) 获取 Chat ID
3. 大陆部署使用 `https://tg-api.xxx.com`

**IP Provider**:
- 配置 `DirectIpApiUrl` 优先使用（性能更好）
- 或配置设备组API账号信息

## 日志管理

### 查看日志

```bash
# 实时查看
tail -f /opt/dns-disaster/logs/dns-disaster-$(date +%Y%m%d).log

# 查看最近100行
tail -n 100 /opt/dns-disaster/logs/dns-disaster-$(date +%Y%m%d).log

# 搜索错误
grep "ERR" /opt/dns-disaster/logs/dns-disaster-*.log
```

### 日志配置
- 位置：`logs/dns-disaster-YYYYMMDD.log`
- 滚动：每天创建新文件
- 保留：30天
- 大小：单文件最大10MB

详细说明见 [LOGGING.md](LOGGING.md)

## 常用命令

```bash
# 服务管理
sudo systemctl start dns-disaster    # 启动
sudo systemctl stop dns-disaster     # 停止
sudo systemctl restart dns-disaster  # 重启
sudo systemctl status dns-disaster   # 状态

# 日志查看
tail -f /opt/dns-disaster/logs/dns-disaster-$(date +%Y%m%d).log
sudo journalctl -u dns-disaster -f

# 更新程序
sudo systemctl stop dns-disaster
sudo cp /tmp/DNSDisaster /opt/dns-disaster/
sudo chmod +x /opt/dns-disaster/DNSDisaster
sudo systemctl start dns-disaster
```

## 故障排查

### ICU库缺失错误

```
Couldn't find a valid ICU package installed on the system
```

**解决**: 使用最新版本（已禁用ICU依赖），无需安装额外库

### 服务无法启动

```bash
# 查看错误日志
sudo journalctl -u dns-disaster -n 50

# 手动运行测试
cd /opt/dns-disaster
./DNSDisaster
```

### 日志文件不存在

```bash
# 创建日志目录
mkdir -p /opt/dns-disaster/logs
chmod 755 /opt/dns-disaster/logs
```

### Telegram通知失败

```bash
# 测试API连接
curl https://tg-api.xxx.com/botYOUR_BOT_TOKEN/getMe

# 检查日志
grep "Telegram" /opt/dns-disaster/logs/dns-disaster-*.log
```

### DNS更新失败

```bash
# 检查Cloudflare配置
curl -X GET "https://api.cloudflare.com/client/v4/zones/YOUR_ZONE_ID" \
  -H "Authorization: Bearer YOUR_API_TOKEN"

# 查看错误日志
grep "Cloudflare" /opt/dns-disaster/logs/dns-disaster-*.log
```

## 验证部署

部署成功后应该：
- ✅ 服务状态为 `active (running)`
- ✅ 收到Telegram启动通知
- ✅ 日志文件正常生成
- ✅ 系统开始监控DNS

## 卸载

```bash
# 停止并禁用服务
sudo systemctl stop dns-disaster
sudo systemctl disable dns-disaster

# 删除文件
sudo rm /etc/systemd/system/dns-disaster.service
sudo rm -rf /opt/dns-disaster

# 重新加载systemd
sudo systemctl daemon-reload
```

## 更多文档

- [README.md](../README.md) - 功能说明和配置详解
- [LOGGING.md](LOGGING.md) - 日志管理详细指南
