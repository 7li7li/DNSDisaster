# DNS灾难恢复系统 - 部署指南

## 快速部署步骤

### 1. 准备发布文件

在Windows上已经完成发布，文件位于：
```
D:\Project\VS\DNSDisaster\DNSDisaster\bin\Release\net8.0\publish\linux-x64\
```

### 2. 上传到Linux服务器

使用SCP或其他工具上传发布文件到服务器：

```bash
# 示例：使用SCP上传
scp -r D:\Project\VS\DNSDisaster\DNSDisaster\bin\Release\net8.0\publish\linux-x64\* user@server:/tmp/dns-disaster/
```

或者使用WinSCP、FileZilla等图形化工具上传。

### 3. 在服务器上部署

SSH连接到服务器后执行：

```bash
# 创建部署目录
sudo mkdir -p /opt/dns-disaster

# 移动文件
sudo mv /tmp/dns-disaster/* /opt/dns-disaster/

# 设置执行权限
sudo chmod +x /opt/dns-disaster/DNSDisaster

# 配置appsettings.json
sudo nano /opt/dns-disaster/appsettings.json
```

### 4. 配置appsettings.json

确保配置文件包含正确的信息：

```json
{
  "DNSDisaster": {
    "PrimaryDomain": "your-domain.com",
    "PrimaryPort": 12345,
    "BackupDomain": "backup-domain.com",
    "CheckIntervalSeconds": 30,
    "FailureThreshold": 3
  },
  "Cloudflare": {
    "ApiToken": "your_cloudflare_api_token",
    "ZoneId": "your_zone_id"
  },
  "Telegram": {
    "BotToken": "your_telegram_bot_token",
    "ChatId": "your_chat_id"
  },
  "IpProvider": {
    "Username": "your_username",
    "Password": "your_password",
    "DeviceGroupId": 1,
    "ApiBaseUrl": "https://nya.trp.sh/api/v1",
    "DirectIpApiUrl": "https://your-api.com/status"
  }
}
```

### 5. 创建systemd服务

创建服务文件：

```bash
sudo nano /etc/systemd/system/dns-disaster.service
```

内容如下：

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
StandardOutput=journal
StandardError=journal
SyslogIdentifier=dns-disaster

[Install]
WantedBy=multi-user.target
```

### 6. 启动服务

```bash
# 重新加载systemd配置
sudo systemctl daemon-reload

# 启用开机自启
sudo systemctl enable dns-disaster

# 启动服务
sudo systemctl start dns-disaster

# 查看状态
sudo systemctl status dns-disaster
```

### 7. 查看日志

**查看文件日志**:
```bash
# 实时查看今天的日志
tail -f /opt/dns-disaster/logs/dns-disaster-$(date +%Y%m%d).log

# 查看最近100行
tail -n 100 /opt/dns-disaster/logs/dns-disaster-$(date +%Y%m%d).log

# 搜索错误
grep "ERR" /opt/dns-disaster/logs/dns-disaster-*.log
```

**查看systemd日志**:
```bash
# 实时查看日志
sudo journalctl -u dns-disaster -f

# 查看最近100行日志
sudo journalctl -u dns-disaster -n 100

# 查看今天的日志
sudo journalctl -u dns-disaster --since today
```

**注意**: 系统会同时输出日志到文件和systemd journal，建议主要查看文件日志，因为它有更好的格式和保留策略。

## 常用命令

```bash
# 启动服务
sudo systemctl start dns-disaster

# 停止服务
sudo systemctl stop dns-disaster

# 重启服务
sudo systemctl restart dns-disaster

# 查看状态
sudo systemctl status dns-disaster

# 查看日志
sudo journalctl -u dns-disaster -f

# 禁用开机自启
sudo systemctl disable dns-disaster
```

## 更新部署

当需要更新程序时：

```bash
# 停止服务
sudo systemctl stop dns-disaster

# 备份当前版本（可选）
sudo cp -r /opt/dns-disaster /opt/dns-disaster.backup

# 上传新版本文件并覆盖
sudo cp /tmp/dns-disaster-new/* /opt/dns-disaster/

# 设置权限
sudo chmod +x /opt/dns-disaster/DNSDisaster

# 启动服务
sudo systemctl start dns-disaster

# 查看日志确认正常运行
sudo journalctl -u dns-disaster -f
```

## 故障排查

### 服务无法启动

1. 检查配置文件是否正确：
```bash
cat /opt/dns-disaster/appsettings.json
```

2. 检查文件权限：
```bash
ls -la /opt/dns-disaster/
```

3. 手动运行查看错误：
```bash
cd /opt/dns-disaster
./DNSDisaster
```

### 网络连接问题

测试API连接：
```bash
# 测试Cloudflare API
curl -H "Authorization: Bearer YOUR_TOKEN" \
  https://api.cloudflare.com/client/v4/zones/YOUR_ZONE_ID

# 测试Telegram API
curl https://api.telegram.org/botYOUR_BOT_TOKEN/getMe

# 测试IP提供商API
curl https://your-api.com/status
```

### 查看详细日志

修改配置文件中的日志级别为Debug：
```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "DNSDisaster": "Debug"
  }
}
```

然后重启服务：
```bash
sudo systemctl restart dns-disaster
```

## 安全建议

1. **保护配置文件**：
```bash
sudo chmod 600 /opt/dns-disaster/appsettings.json
```

2. **使用专用用户**（可选）：
```bash
# 创建专用用户
sudo useradd -r -s /bin/false dns-disaster

# 修改文件所有者
sudo chown -R dns-disaster:dns-disaster /opt/dns-disaster

# 修改服务文件中的User
sudo nano /etc/systemd/system/dns-disaster.service
# 将 User=root 改为 User=dns-disaster
```

3. **定期备份配置**：
```bash
sudo cp /opt/dns-disaster/appsettings.json ~/appsettings.json.backup
```

## 监控建议

1. **设置监控告警**：通过Telegram通知已经实现基本监控

2. **定期检查日志**：
```bash
# 添加到crontab每天检查
0 9 * * * journalctl -u dns-disaster --since "24 hours ago" | grep -i error
```

3. **监控服务状态**：
```bash
# 创建监控脚本
cat > /usr/local/bin/check-dns-disaster.sh << 'EOF'
#!/bin/bash
if ! systemctl is-active --quiet dns-disaster; then
    echo "DNS Disaster service is not running!"
    systemctl start dns-disaster
fi
EOF

chmod +x /usr/local/bin/check-dns-disaster.sh

# 添加到crontab每5分钟检查一次
*/5 * * * * /usr/local/bin/check-dns-disaster.sh
```

## 性能优化

1. **调整检测间隔**：根据实际需求调整 `CheckIntervalSeconds`
2. **使用直接IP查询API**：配置 `DirectIpApiUrl` 以获得更好的性能
3. **合理设置失败阈值**：`FailureThreshold` 建议设置为3-5次

## 卸载

如果需要完全卸载：

```bash
# 停止并禁用服务
sudo systemctl stop dns-disaster
sudo systemctl disable dns-disaster

# 删除服务文件
sudo rm /etc/systemd/system/dns-disaster.service

# 删除程序文件
sudo rm -rf /opt/dns-disaster

# 重新加载systemd
sudo systemctl daemon-reload
```
