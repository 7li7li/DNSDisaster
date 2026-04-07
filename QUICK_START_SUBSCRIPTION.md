# 套餐监控功能快速入门

## 5分钟快速启用

### 步骤 1: 更新配置文件

编辑 `DNSDisaster/appsettings.json`，添加 `SubscriptionMonitorTasks` 配置：

```json
{
  "MonitorTasks": [
    // 现有的DNS监控任务（可选）
  ],
  "SubscriptionMonitorTasks": [
    {
      "Name": "Subscription-YourName",
      "CheckIntervalHours": 6,
      "ApiSettings": {
        "Username": "your_username",
        "Password": "your_password",
        "ApiBaseUrl": "https://api.example.com/v1"
      }
    }
  ],
  "Cloudflare": {
    // 如果只用套餐监控，可以留空
  },
  "Telegram": {
    // 必填
  }
}
```

**重要说明**：
- DNS监控和套餐监控现在是独立配置的
- 可以只配置套餐监控，不需要DNS监控
- 可以监控多个账户，只需添加多个任务

### 步骤 2: 重新编译（如果需要）

```bash
cd DNSDisaster
dotnet publish -c Release -r linux-x64 --self-contained
```

### 步骤 3: 重启服务

```bash
# 使用 systemd
sudo systemctl restart dns-disaster

# 或直接运行
./DNSDisaster
```

### 步骤 4: 验证功能

查看日志确认套餐监控已启动：

```bash
tail -f logs/dns-disaster-$(date +%Y%m%d).log | grep "套餐"
```

你应该看到类似这样的日志：

```
[2026-04-01 10:30:00 INF] [DNSDisaster] 开始检查套餐状态...
[2026-04-01 10:30:01 INF] [DNSDisaster] 套餐状态 - 到期时间: 2026-05-01 10:30:00, 流量: 45.50/100.00 GB, 余额: 50.00 元
```

## 测试 API 接口

在启用功能前，建议先测试 API 接口是否正常：

### Linux/Mac

```bash
chmod +x test_subscription_api.sh
./test_subscription_api.sh
```

### Windows

```powershell
.\test_subscription_api.ps1
```

记得先修改脚本中的用户名和密码！

## 配置说明

### Name

- **类型**: 字符串
- **必填**: 是
- **说明**: 任务名称，用于日志和通知标识

### CheckIntervalHours

- **类型**: 整数
- **默认**: 6
- **推荐**: 6-12 小时
- **说明**: 多久检查一次套餐状态

### ApiSettings

- **类型**: 对象
- **必填**: 是
- **说明**: API认证信息，用于获取用户套餐数据
- **包含字段**:
  - `Username`: API用户名
  - `Password`: API密码
  - `ApiBaseUrl`: API基础URL

注意：套餐监控不需要 `DeviceGroupId` 和 `DirectIpApiUrl` 配置。

## 通知触发条件

系统会在以下情况发送 Telegram 通知：

| 条件 | 阈值 | 示例 |
|------|------|------|
| 套餐即将到期 | ≤ 3 天（紧急）或 ≤ 7 天（警告） | "套餐将在 2.5 天后到期" |
| 流量即将用尽 | ≥ 90%（紧急）或 ≥ 80%（警告） | "流量已使用 92.5%" |
| 余额不足续费 | 余额 < 续费价格 且（套餐 ≤ 3天 或 流量 ≥ 90%） | "余额 0.22 元，续费需要 229.00 元" |

**重要说明**：
- 只有达到紧急阈值（3天内到期、流量超90%）才会发送通知
- 余额不足只在套餐快到期或流量不足时才会告警
- 每次通知都包含完整的套餐信息（用户、套餐、到期时间、流量、余额）
- 24小时内同一任务只会发送一次通知（冷却期）

## 通知示例

```
🔔 DNS灾难恢复系统通知

⚠️ 套餐状态警告 [Subscription-User1]
👤 用户: user123
📦 套餐: Premium Plan
⏰ 到期时间: 2026-04-04 10:30:00 (剩余 2.5 天)
📊 流量使用: 1850.00/2048.00 GB (90.3%)
💰 余额: 0.22 元 (续费需要 229.00 元)

告警详情:
⚠️ 套餐将在 2.5 天后到期
⚠️ 流量即将用尽
⚠️ 余额不足以续费

⏰ 时间: 2026-04-01 10:30:00
```

## 常见问题

### Q: 会不会频繁收到通知？

A: 不会。系统有 24 小时的通知冷却期，同一警告在 24 小时内只会发送一次。

### Q: 如果 API 接口不可用会怎样？

A: 系统会记录错误日志，但不会影响主要的 DNS 监控功能。

### Q: 可以只监控某些任务的套餐吗？

A: 可以。每个监控任务可以独立配置是否启用套餐监控。

### Q: 检查间隔设置多少合适？

A: 推荐 6-12 小时。太频繁会增加 API 调用次数，太长可能错过重要警告。

### Q: 如何关闭套餐监控？

A: 从 `SubscriptionMonitorTasks` 数组中删除对应的任务配置，或将数组设置为空 `[]`。

## 故障排查

### 没有看到套餐检查日志

1. 确认配置已正确设置：
   ```json
   "SubscriptionMonitorTasks": [
     {
       "Name": "YourTask",
       "CheckIntervalHours": 6,
       "ApiSettings": { /* ... */ }
     }
   ]
   ```

2. 检查是否到了检查时间（默认 6 小时检查一次）

3. 查看错误日志：
   ```bash
   grep "ERR" logs/dns-disaster-*.log | grep "套餐"
   ```

### API 调用失败

1. 测试登录接口：
   ```bash
   curl -X POST https://api.example.com/v1/auth/login \
     -H "Content-Type: application/json" \
     -d '{"username":"your_username","password":"your_password"}'
   ```

2. 检查用户名和密码是否正确

3. 确认 API 地址是否正确（注意是 `/api/v1` 不是 `/v1`）

### 没有收到通知

1. 检查是否达到紧急阈值（7天、90%、余额不足）

2. 确认不在 24 小时冷却期内

3. 验证 Telegram Bot 配置是否正确

## 更多帮助

- 配置指南: [CONFIGURATION_GUIDE.md](CONFIGURATION_GUIDE.md)
- 完整文档: [SUBSCRIPTION_MONITORING.md](DNSDisaster/SUBSCRIPTION_MONITORING.md)
- 主文档: [README.md](README.md)
- 发布说明: [SUBSCRIPTION_FEATURE_RELEASE.md](SUBSCRIPTION_FEATURE_RELEASE.md)

## 反馈

如有问题或建议，欢迎提交 Issue 或 Pull Request。
