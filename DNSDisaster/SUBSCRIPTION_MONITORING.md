# 套餐监控功能说明

## 功能概述

套餐监控功能会定期检查 API 的用户信息，监控以下内容：

1. **套餐到期时间** - 检测套餐是否即将到期
2. **流量使用情况** - 检测流量是否即将用尽
3. **钱包余额** - 检测余额是否足够下次续费

当检测到以下紧急情况时，会通过 Telegram Bot 发送通知：

- 套餐将在 7 天内到期
- 流量使用超过 90%
- 余额不足以支付下次续费费用

## 配置说明

在 `appsettings.json` 的每个监控任务的 `IpProvider` 配置中添加以下选项：

```json
{
  "MonitorTasks": [
    {
      "Name": "Task1",
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

### 配置参数说明

- `EnableSubscriptionMonitoring`: 是否启用套餐监控（默认：false）
- `SubscriptionCheckIntervalHours`: 检查间隔（小时），默认 6 小时

## API 接口要求

套餐监控功能需要 API 提供以下接口：

### 1. 登录接口
- **URL**: `{ApiBaseUrl}/auth/login`
- **方法**: POST
- **请求体**:
```json
{
  "username": "your_username",
  "password": "your_password"
}
```
- **响应**:
```json
{
  "data": "token_string"
}
```

### 2. 用户信息接口
- **URL**: `{ApiBaseUrl}/user/info`
- **方法**: GET
- **请求头**: `Authorization: {token}`
- **响应**:
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

### 响应字段说明

- `expire`: 套餐到期时间（Unix 时间戳，秒）
- `traffic_used`: 已使用流量（字节）
- `traffic_enable`: 总流量（字节）
- `balance`: 钱包余额（字符串格式，元）
- `renew_price`: 续费价格（字符串格式，元）

## 通知规则

### 警告级别

系统会在以下情况发送通知：

1. **套餐到期警告**
   - 14 天内到期：普通警告
   - 7 天内到期：紧急警告（触发通知）

2. **流量使用警告**
   - 使用超过 80%：普通警告
   - 使用超过 90%：紧急警告（触发通知）

3. **余额不足警告**
   - 余额 < 续费价格：紧急警告（触发通知）

### 通知冷却期

为避免频繁通知，系统设置了 24 小时的通知冷却期。同一任务的套餐警告在 24 小时内只会发送一次。

## 通知示例

```
⚠️ 套餐状态警告 [Task1]
👤 用户: user123
📦 套餐: Premium Plan

⏰ 套餐将在 5.2 天后到期（2026-04-06 10:30:00）
📊 流量使用: 1600.00/2048.00 GB (78.1%)
💰 余额不足：当前余额 0.22 元，续费需要 229.00 元

⏰ 时间: 2026-04-01 10:30:00
```

## 日志输出

套餐监控会在日志中输出详细信息：

```
[2026-04-01 10:30:00 INF] [Task1] 套餐状态 - 到期时间: 2026-04-06 10:30:00, 流量: 92.50/100.00 GB, 余额: 15.50 元
[2026-04-01 10:30:00 INF] [Task1] 已发送套餐警告通知
```

## 注意事项

1. 套餐监控功能是可选的，不会影响主要的 DNS 监控功能
2. 如果 API 接口不可用或返回错误，系统会记录日志但不会中断主监控流程
3. 每个监控任务可以独立配置是否启用套餐监控
4. 建议检查间隔设置为 6-12 小时，避免过于频繁的 API 调用
