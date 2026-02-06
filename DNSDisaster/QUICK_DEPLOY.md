# 快速部署指南

## 📦 发布信息

- **发布时间**: 2026-02-06 16:49
- **文件大小**: 64.93 MB
- **发布位置**: `DNSDisaster\bin\Release\net8.0\publish\linux-x64-single\`

## 🚀 快速部署步骤

### 1. 上传文件到服务器

使用SCP或其他工具上传以下文件：

```bash
# 上传可执行文件
scp DNSDisaster\bin\Release\net8.0\publish\linux-x64-single\DNSDisaster user@server:/tmp/

# 上传配置文件
scp DNSDisaster\bin\Release\net8.0\publish\linux-x64-single\appsettings.json user@server:/tmp/
```

### 2. 在服务器上部署

SSH连接到服务器后执行：

```bash
# 创建部署目录
sudo mkdir -p /opt/dns-disaster

# 移动文件
sudo mv /tmp/DNSDisaster /opt/dns-disaster/
sudo mv /tmp/appsettings.json /opt/dns-disaster/

# 设置执行权限
sudo chmod +x /opt/dns-disaster/DNSDisaster

# 创建日志目录
sudo mkdir -p /opt/dns-disaster/logs
sudo chmod 755 /opt/dns-disaster/logs
```

### 3. 配置appsettings.json

```bash
sudo nano /opt/dns-disaster/appsettings.json
```

确保配置正确：
- Cloudflare API Token 和 Zone ID
- Telegram Bot Token 和 Chat ID
- IP Provider 配置
- 主域名和备用域名

### 4. 测试运行

```bash
cd /opt/dns-disaster
./DNSDisaster
```

按 `Ctrl+C` 停止测试。

### 5. 创建systemd服务

```bash
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
StandardOutput=journal
StandardError=journal
SyslogIdentifier=dns-disaster

[Install]
WantedBy=multi-user.target
```

### 6. 启动服务

```bash
# 重新加载systemd
sudo systemctl daemon-reload

# 启用开机自启
sudo systemctl enable dns-disaster

# 启动服务
sudo systemctl start dns-disaster

# 查看状态
sudo systemctl status dns-disaster
```

### 7. 查看日志

```bash
# 查看文件日志（推荐）
tail -f /opt/dns-disaster/logs/dns-disaster-$(date +%Y%m%d).log

# 或查看systemd日志
sudo journalctl -u dns-disaster -f
```

## ✅ 验证部署

检查以下内容确认部署成功：

1. **服务状态**: `sudo systemctl status dns-disaster` 显示 `active (running)`
2. **日志文件**: `ls -la /opt/dns-disaster/logs/` 有今天的日志文件
3. **Telegram通知**: 应该收到系统启动通知
4. **进程运行**: `ps aux | grep DNSDisaster` 显示进程

## 🔧 常用命令

```bash
# 启动服务
sudo systemctl start dns-disaster

# 停止服务
sudo systemctl stop dns-disaster

# 重启服务
sudo systemctl restart dns-disaster

# 查看状态
sudo systemctl status dns-disaster

# 查看实时日志
tail -f /opt/dns-disaster/logs/dns-disaster-$(date +%Y%m%d).log

# 搜索错误
grep "ERR" /opt/dns-disaster/logs/dns-disaster-*.log

# 查看最近100行
tail -n 100 /opt/dns-disaster/logs/dns-disaster-$(date +%Y%m%d).log
```

## 📝 新功能

此版本包含以下新功能：

1. **文件日志** - 自动写入日志到 `logs/` 目录
2. **日志滚动** - 每天创建新文件，保留30天
3. **自定义Telegram API** - 支持大陆可访问的API地址
4. **优化的监控逻辑** - 只在首次检测DNS一致性
5. **移除Telegram.Bot依赖** - 使用HttpClient直接调用API

## 🆘 故障排查

### 服务无法启动

```bash
# 查看详细错误
sudo journalctl -u dns-disaster -n 50

# 手动运行查看错误
cd /opt/dns-disaster
./DNSDisaster
```

### 日志文件不存在

```bash
# 检查目录权限
ls -la /opt/dns-disaster/

# 创建日志目录
sudo mkdir -p /opt/dns-disaster/logs
sudo chmod 755 /opt/dns-disaster/logs
```

### Telegram通知不工作

```bash
# 测试API连接
curl https://tg-api.7li7li.com/botYOUR_BOT_TOKEN/getMe

# 查看日志中的错误
grep "Telegram" /opt/dns-disaster/logs/dns-disaster-*.log
```

## 📚 更多文档

- **完整部署指南**: `DEPLOY.md`
- **日志管理**: `LOGGING.md`
- **使用说明**: `README.md`

## 🎉 部署完成

如果一切正常，你应该：
- ✅ 收到Telegram启动通知
- ✅ 看到日志文件在 `/opt/dns-disaster/logs/`
- ✅ 服务状态显示 `active (running)`
- ✅ 系统开始监控DNS状态

祝使用愉快！
