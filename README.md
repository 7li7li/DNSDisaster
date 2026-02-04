# DNS灾难恢复系统

这是一个自动化的DNS故障转移系统，当主域名服务不可用时自动切换到备用域名，并在服务恢复时自动切换回来。

## 功能特性

- **自动监控**: 定期使用TCPing检测主域名端口连通性
- **故障转移**: 连续失败达到阈值时自动切换到CNAME备用域名
- **自动恢复**: 检测到原始服务恢复时自动切换回A记录
- **Telegram通知**: 实时通知故障转移和恢复状态
- **Cloudflare集成**: 通过API自动管理DNS记录

## 配置说明

### 1. 修改 appsettings.json

```json
{
  "DNSDisaster": {
    "PrimaryDomain": "a.com",           // 主域名
    "PrimaryPort": 10000,               // 监控端口
    "BackupDomain": "b.com",            // 备用域名
    "CheckIntervalSeconds": 30,         // 检查间隔(秒)
    "FailureThreshold": 3,              // 故障转移阈值
    "RecoveryCheckIntervalSeconds": 60  // 恢复检查间隔(秒)
  },
  "Cloudflare": {
    "ApiToken": "YOUR_CLOUDFLARE_API_TOKEN",  // Cloudflare API令牌
    "ZoneId": "YOUR_ZONE_ID",                 // 区域ID
    "RecordName": "a.com"                     // DNS记录名称
  },
  "Telegram": {
    "BotToken": "YOUR_TELEGRAM_BOT_TOKEN",    // Telegram机器人令牌
    "ChatId": "YOUR_CHAT_ID"                  // 聊天ID
  }
}
```

### 2. 获取Cloudflare配置

1. 登录 [Cloudflare Dashboard](https://dash.cloudflare.com/)
2. 选择你的域名
3. 在右侧边栏找到 **Zone ID** 并复制
4. 转到 **My Profile** > **API Tokens**
5. 创建自定义令牌，权限设置为：
   - Zone:Zone:Read
   - Zone:DNS:Edit
   - 指定你的域名区域

### 3. 获取Telegram配置

1. 与 [@BotFather](https://t.me/botfather) 对话创建机器人
2. 获取 **Bot Token**
3. 将机器人添加到群组或获取个人聊天ID
4. 发送消息给 [@userinfobot](https://t.me/userinfobot) 获取 **Chat ID**

## 运行方式

### 开发环境
```bash
dotnet run
```

### 生产环境
```bash
dotnet publish -c Release -r linux-x64 --self-contained
./DNSDisaster
```

## 工作流程

1. **正常监控**: 每30秒检查主域名端口连通性
2. **故障检测**: 连续3次失败后触发故障转移
3. **故障转移**: 将DNS记录从A记录切换为CNAME指向备用域名
4. **恢复检测**: 每60秒检查原始IP是否恢复
5. **自动恢复**: 检测到恢复后自动切换回A记录

## 通知消息

系统会发送以下类型的Telegram通知：
- 🔔 系统启动通知
- ⚠️ 故障转移通知
- ✅ 服务恢复通知
- ❌ 错误通知

## 注意事项

1. 确保Cloudflare API令牌有足够的权限
2. 备用域名应该指向可用的服务
3. 建议设置较短的TTL(300秒)以加快DNS传播
4. 监控日志以确保系统正常运行
5. 定期测试故障转移功能

## 故障排除

### 常见问题

1. **无法连接Cloudflare API**
   - 检查API令牌是否正确
   - 确认Zone ID是否匹配
   - 验证网络连接

2. **Telegram通知不工作**
   - 检查Bot Token是否有效
   - 确认Chat ID是否正确
   - 确保机器人有发送消息权限

3. **DNS切换不生效**
   - 检查DNS传播时间
   - 验证记录名称是否正确
   - 确认TTL设置

### 日志级别

可以在appsettings.json中调整日志级别：
```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "DNSDisaster": "Debug"
  }
}
```

## 许可证

MIT License