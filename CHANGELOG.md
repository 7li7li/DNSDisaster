# 更新日志

## [最新版本] - 2026-04-07

### 新增功能

#### 套餐监控功能
- ✅ 自动监控套餐到期时间、流量使用和余额状态
- ✅ 支持监控多个账户
- ✅ 通过Telegram发送告警通知
- ✅ 每个任务独立配置启用/禁用开关

#### 配置优化
- ✅ DNS监控和套餐监控完全独立配置
- ✅ 每个任务支持独立的 `Enabled` 开关
- ✅ 套餐监控使用简化的 `ApiSettings` 配置（不需要DeviceGroupId）

### 功能改进

#### 告警规则优化
- 🔄 套餐到期告警：从14天/7天改为7天/3天
- 🔄 流量使用告警：保持80%/90%阈值
- 🔄 余额不足告警：只在套餐≤3天或流量≥90%时触发
- 🔄 只有达到紧急阈值才发送通知

#### 通知内容增强
- ✨ 每次通知包含完整的套餐信息
- ✨ 显示用户名和套餐名称
- ✨ 显示到期时间和剩余天数
- ✨ 显示流量使用详情（已用/总量/百分比）
- ✨ 显示余额和续费价格
- ✨ 列出具体的告警详情

### 配置示例

#### DNS监控任务
```json
{
  "Name": "DNSDisaster",
  "Enabled": true,
  "PrimaryDomain": "example.com",
  "PrimaryPort": 443,
  "BackupDomain": "backup.example.com",
  "CheckIntervalSeconds": 30,
  "FailureThreshold": 3,
  "IpProvider": {
    "Username": "your_username",
    "Password": "your_password",
    "DeviceGroupId": 1,
    "ApiBaseUrl": "https://api.example.com/v1"
  }
}
```

#### 套餐监控任务
```json
{
  "Name": "Subscription-User1",
  "Enabled": true,
  "CheckIntervalHours": 6,
  "ApiSettings": {
    "Username": "your_username",
    "Password": "your_password",
    "ApiBaseUrl": "https://api.example.com/v1"
  }
}
```

### 通知示例

```
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

### 文档更新

- 📝 新增 `CONFIGURATION_GUIDE.md` - 详细配置指南
- 📝 新增 `PRIVACY_NOTICE.md` - 隐私和安全说明
- 📝 更新 `SUBSCRIPTION_MONITORING.md` - 套餐监控详细说明
- 📝 更新 `QUICK_START_SUBSCRIPTION.md` - 快速入门指南
- 📝 更新 `README.md` - 主文档

### 技术改进

- 🔧 优化配置模型，分离DNS和套餐监控配置
- 🔧 改进日志输出，包含更详细的套餐信息
- 🔧 增强配置验证，检查至少有一个启用的任务
- 🔧 支持任务级别的启用/禁用控制

### 破坏性变更

⚠️ **配置文件格式变更**

如果你使用了套餐监控功能，需要更新配置：

**旧格式**：
```json
"SubscriptionMonitorTasks": [
  {
    "Name": "Task1",
    "CheckIntervalHours": 6,
    "IpProvider": {
      "Username": "user",
      "Password": "pass",
      "DeviceGroupId": 1,
      "ApiBaseUrl": "https://api.example.com/v1"
    }
  }
]
```

**新格式**：
```json
"SubscriptionMonitorTasks": [
  {
    "Name": "Task1",
    "Enabled": true,
    "CheckIntervalHours": 6,
    "ApiSettings": {
      "Username": "user",
      "Password": "pass",
      "ApiBaseUrl": "https://api.example.com/v1"
    }
  }
]
```

变更说明：
1. 添加 `Enabled` 字段（可选，默认true）
2. `IpProvider` 改为 `ApiSettings`
3. 移除不需要的 `DeviceGroupId` 字段

### 升级指南

1. 备份当前配置文件
2. 更新配置文件格式（参考上面的新格式）
3. 为每个任务添加 `Enabled` 字段（可选）
4. 重启服务

### 已知问题

无

### 下一步计划

- [ ] 支持自定义告警阈值
- [ ] 支持多个Telegram通知目标
- [ ] 添加Web管理界面
- [ ] 支持更多API提供商

---

## 贡献

欢迎提交Issue和Pull Request！

## 许可证

MIT License
