# 套餐监控功能发布说明

## 新增功能

本次更新为 DNS灾难恢复系统 添加了套餐监控功能，可以自动监控账户的套餐状态。

## 功能亮点

### 1. 自动监控套餐状态
- **套餐到期监控**：自动检测套餐到期时间，提前 7-14 天发送警告
- **流量使用监控**：实时监控流量使用情况，使用超过 80%-90% 时发送警告
- **余额监控**：检查钱包余额是否足够下次续费

### 2. 智能通知机制
- 只在紧急情况下发送通知（7天内到期、流量超90%、余额不足）
- 24小时通知冷却期，避免频繁打扰
- 通过 Telegram Bot 发送详细的警告信息

### 3. 灵活配置
- 可以为每个监控任务独立配置是否启用套餐监控
- 可自定义检查间隔（默认 6 小时）
- 不影响主要的 DNS 监控功能

## 使用方法

### 1. 更新配置文件

在 `appsettings.json` 的 `IpProvider` 配置中添加以下两个参数：

```json
{
  "MonitorTasks": [
    {
      "Name": "DNSDisaster",
      "IpProvider": {
        "Username": "your_username",
        "Password": "your_password",
        "ApiBaseUrl": "https://api.example.com/v1",
        "EnableSubscriptionMonitoring": true,
        "SubscriptionCheckIntervalHours": 6
      }
    }
  ]
}
```

### 2. 重启服务

```bash
# 如果使用 systemd
sudo systemctl restart dns-disaster

# 或者直接运行
./DNSDisaster
```

### 3. 查看日志

```bash
# 查看套餐监控日志
tail -f logs/dns-disaster-$(date +%Y%m%d).log | grep "套餐"
```

## 通知示例

当检测到问题时，你会收到类似这样的 Telegram 通知：

```
⚠️ 套餐状态警告 [DNSDisaster]

⏰ 套餐将在 5.2 天后到期（2026-04-06 10:30:00）
📊 流量已使用 92.5% (92.50/100.00 GB)
💰 余额不足：当前余额 15.50 元，续费需要 20.00 元

⏰ 时间: 2026-04-01 10:30:00
```

## API 要求

套餐监控功能需要 API 提供以下接口：

### 用户信息接口
- **URL**: `https://api.example.com/v1/user/info`
- **方法**: GET
- **认证**: Bearer Token（通过登录接口获取）

### 响应格式
```json
{
  "code": 0,
  "data": {
    "expire": 1776084708,
    "traffic_used": 1717189077802,
    "traffic_enable": 2199023255552,
    "balance": "0.22",
    "renew_price": "229"
  },
  "msg": ""
}
```

## 配置参数说明

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| EnableSubscriptionMonitoring | bool | false | 是否启用套餐监控 |
| SubscriptionCheckIntervalHours | int | 6 | 检查间隔（小时） |

## 警告阈值

| 监控项 | 警告阈值 | 紧急阈值 |
|--------|----------|----------|
| 套餐到期 | 14天 | 7天 |
| 流量使用 | 80% | 90% |
| 余额 | - | 小于续费价格 |

只有达到紧急阈值时才会发送 Telegram 通知。

## 技术实现

### 新增文件
- `DNSDisaster/Services/SubscriptionMonitorService.cs` - 套餐监控服务
- `DNSDisaster/SUBSCRIPTION_MONITORING.md` - 详细文档

### 修改文件
- `DNSDisaster/Models/AppSettings.cs` - 添加配置模型
- `DNSDisaster/Services/DnsMonitoringService.cs` - 集成套餐监控
- `DNSDisaster/Program.cs` - 注册新服务
- `DNSDisaster/appsettings.example.json` - 更新配置示例
- `README.md` - 更新主文档

## 注意事项

1. **可选功能**：套餐监控是完全可选的，不启用不会影响 DNS 监控功能
2. **API 兼容性**：如果 API 接口不可用或格式不匹配，会记录错误但不会中断主流程
3. **通知频率**：建议保持默认的 6 小时检查间隔和 24 小时通知冷却期
4. **隐私安全**：套餐信息仅在本地处理，不会发送到第三方

## 故障排查

### 套餐监控不工作

1. 检查配置是否正确启用：
```json
"EnableSubscriptionMonitoring": true
```

2. 查看日志中是否有错误：
```bash
grep "套餐" logs/dns-disaster-*.log
```

3. 手动测试 API 接口：
```bash
# 先登录获取 token
curl -X POST https://api.example.com/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"your_username","password":"your_password"}'

# 使用 token 查询用户信息
curl -X GET https://api.example.com/v1/user/info \
  -H "Authorization: YOUR_TOKEN"
```

### 没有收到通知

1. 检查是否达到紧急阈值（7天内到期、流量超90%、余额不足）
2. 检查是否在 24 小时通知冷却期内
3. 验证 Telegram Bot 配置是否正确

## 更多信息

详细文档请参考：
- [SUBSCRIPTION_MONITORING.md](DNSDisaster/SUBSCRIPTION_MONITORING.md) - 完整功能说明
- [README.md](README.md) - 系统总体文档

## 反馈

如有问题或建议，欢迎提交 Issue。
