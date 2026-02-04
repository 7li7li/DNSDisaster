# DNS灾难恢复系统 - 安装配置指南

## 快速开始

### 1. 环境要求
- .NET 8.0 Runtime 或 SDK
- Windows 或 Linux 系统
- 网络连接（访问Cloudflare API和Telegram API）

### 2. 配置步骤

#### 步骤1: 复制配置文件
```bash
cp appsettings.example.json appsettings.json
```

#### 步骤2: 获取Cloudflare配置

1. **登录Cloudflare Dashboard**
   - 访问 https://dash.cloudflare.com/
   - 选择你的域名

2. **获取Zone ID**
   - 在域名概览页面右侧找到"Zone ID"
   - 复制这个ID

3. **创建API Token**
   - 点击右上角头像 → "My Profile"
   - 选择"API Tokens"标签
   - 点击"Create Token"
   - 选择"Custom token"
   - 配置权限：
     ```
     Zone - Zone:Read
     Zone - DNS:Edit
     ```
   - Zone Resources: Include - Specific zone - 选择你的域名
   - 点击"Continue to summary"
   - 点击"Create Token"
   - 复制生成的token

#### 步骤3: 获取Telegram配置

1. **创建Telegram Bot**
   - 在Telegram中搜索 @BotFather
   - 发送 `/newbot` 命令
   - 按提示设置bot名称和用户名
   - 复制获得的Bot Token

2. **获取Chat ID**
   - 将bot添加到你的群组或私聊
   - 在Telegram中搜索 @userinfobot
   - 发送任意消息获取你的Chat ID
   - 或者访问: `https://api.telegram.org/bot<YourBOTToken>/getUpdates`

#### 步骤4: 编辑配置文件

编辑 `appsettings.json`:

```json
{
  "DNSDisaster": {
    "PrimaryDomain": "your-domain.com",      // 你的主域名
    "PrimaryPort": 443,                      // 监控端口
    "BackupDomain": "backup.your-domain.com", // 备用域名
    "CheckIntervalSeconds": 30,              // 检查间隔
    "FailureThreshold": 3,                   // 失败阈值
    "RecoveryCheckIntervalSeconds": 60       // 恢复检查间隔
  },
  "Cloudflare": {
    "ApiToken": "your_api_token_here",       // 步骤2获取的token
    "ZoneId": "your_zone_id_here",           // 步骤2获取的zone id
    "RecordName": "your-domain.com"          // DNS记录名称
  },
  "Telegram": {
    "BotToken": "your_bot_token_here",       // 步骤3获取的bot token
    "ChatId": "your_chat_id_here"            // 步骤3获取的chat id
  }
}
```

### 3. 运行方式

#### Windows
```cmd
# 开发环境
dotnet run

# 或使用批处理脚本
start.bat

# 生产环境
dotnet publish -c Release -r win-x64 --self-contained
cd bin\Release\net8.0\win-x64\publish
DNSDisaster.exe
```

#### Linux
```bash
# 开发环境
dotnet run

# 或使用shell脚本
chmod +x start.sh
./start.sh

# 生产环境部署
chmod +x deploy.sh
sudo ./deploy.sh

# 启动服务
sudo systemctl start dns-disaster
sudo systemctl enable dns-disaster  # 开机自启

# 查看状态和日志
sudo systemctl status dns-disaster
sudo journalctl -u dns-disaster -f
```

## 工作原理

### 监控流程
1. **正常状态**: 系统每30秒检查主域名端口连通性
2. **故障检测**: 连续3次失败后触发故障转移
3. **故障转移**: 将DNS从A记录切换为CNAME指向备用域名
4. **恢复监控**: 每60秒检查原始IP是否恢复
5. **自动恢复**: 检测到恢复后自动切换回A记录

### 通知类型
- 🔔 系统启动
- ⚠️ 故障转移 (A记录 → CNAME)
- ✅ 服务恢复 (CNAME → A记录)
- ❌ 系统错误

## 高级配置

### 自定义检查间隔
```json
{
  "DNSDisaster": {
    "CheckIntervalSeconds": 15,        // 更频繁的检查
    "FailureThreshold": 5,             // 更高的容错
    "RecoveryCheckIntervalSeconds": 30  // 更快的恢复检测
  }
}
```

### 日志级别调整
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "DNSDisaster": "Debug",          // 详细调试信息
      "System.Net.Http": "Warning"     // 减少HTTP日志
    }
  }
}
```

## 故障排除

### 常见问题

1. **Cloudflare API错误**
   ```
   错误: 401 Unauthorized
   解决: 检查API Token是否正确，确认权限设置
   ```

2. **Telegram通知失败**
   ```
   错误: 400 Bad Request
   解决: 检查Bot Token和Chat ID是否正确
   ```

3. **DNS切换不生效**
   ```
   原因: DNS传播需要时间
   解决: 等待5-10分钟，或使用dig/nslookup检查
   ```

4. **端口连接失败**
   ```
   原因: 防火墙或网络问题
   解决: 检查网络连接和防火墙设置
   ```

### 测试命令

```bash
# 测试TCP连接
telnet your-domain.com 443

# 检查DNS记录
nslookup your-domain.com
dig your-domain.com

# 测试Telegram Bot
curl -X POST "https://api.telegram.org/bot<TOKEN>/sendMessage" \
     -H "Content-Type: application/json" \
     -d '{"chat_id":"<CHAT_ID>","text":"Test message"}'
```

## 监控建议

1. **设置合理的阈值**: 避免因网络抖动导致的误切换
2. **监控日志**: 定期检查系统日志确保正常运行
3. **测试故障转移**: 定期手动测试确保系统可用
4. **备用域名准备**: 确保备用域名指向可用的服务
5. **TTL设置**: 建议设置较短的TTL(300秒)以加快切换

## 安全注意事项

1. **API Token安全**: 
   - 使用最小权限原则
   - 定期轮换token
   - 不要在代码中硬编码

2. **配置文件保护**:
   ```bash
   chmod 600 appsettings.json  # 仅所有者可读写
   ```

3. **网络安全**:
   - 使用HTTPS连接
   - 考虑使用VPN或专用网络
   - 监控异常访问

## 性能优化

1. **并发检查**: 系统使用异步操作，支持高并发
2. **资源使用**: 内存占用约20-50MB
3. **网络优化**: 使用连接池减少延迟
4. **日志管理**: 定期清理日志文件

## 扩展功能

可以考虑添加的功能：
- 多域名支持
- 邮件通知
- Web管理界面
- 健康检查API
- 指标监控集成
- 多云DNS支持