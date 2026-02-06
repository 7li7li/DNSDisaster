# DNS灾难恢复系统

这是一个自动化的DNS故障转移系统，通过持续监控动态IP的可用性，自动更新DNS记录，确保域名始终指向可用的服务器。

## 功能特性

- **动态IP监控**: 通过API实时获取最新IP地址
- **智能TCPing检测**: 持续检测IP端口连通性
- **自动DNS更新**: IP可达时自动检查并更新A记录
- **故障转移**: IP不可达时自动切换到CNAME备用域名
- **持续恢复检测**: 即使在CNAME状态也持续监控新IP
- **Telegram通知**: 实时通知DNS变更和系统状态
- **Cloudflare集成**: 通过API自动管理DNS记录
- **双API支持**: 支持直接IP查询API和设备组API

## 配置说明

### 1. 修改 appsettings.json

```json
{
  "DNSDisaster": {
    "PrimaryDomain": "a.com",           // 主域名
    "PrimaryPort": 10000,               // 监控端口
    "BackupDomain": "b.com",            // 备用域名（CNAME目标）
    "CheckIntervalSeconds": 30,         // 检查间隔(秒)
    "FailureThreshold": 3               // 故障转移阈值
  },
  "Cloudflare": {
    "ApiToken": "YOUR_CLOUDFLARE_API_TOKEN",  // Cloudflare API令牌
    "ZoneId": "YOUR_ZONE_ID"                  // 区域ID
  },
  "Telegram": {
    "BotToken": "YOUR_TELEGRAM_BOT_TOKEN",    // Telegram机器人令牌
    "ChatId": "YOUR_CHAT_ID"                  // 聊天ID
    "ApiBaseUrl": "https://tg-api.7li7li.com" // Telegram API地址（大陆可访问）
  },
  "IpProvider": {
    "Username": "your_username",                           // nya.trp.sh 用户名
    "Password": "your_password",                           // nya.trp.sh 密码
    "DeviceGroupId": 1,                                    // 设备组ID
    "ApiBaseUrl": "https://nya.trp.sh/api/v1",            // API基础URL
    "DirectIpApiUrl": "https://your-api.com/status"       // 直接IP查询API（可选）
  }
}
```

### 配置说明

#### DNSDisaster 配置
- `PrimaryDomain`: 要监控和管理的主域名
- `PrimaryPort`: TCPing检测的目标端口
- `BackupDomain`: 故障时切换的备用域名（CNAME目标）
- `CheckIntervalSeconds`: 每次检测的间隔时间
- `FailureThreshold`: 触发故障转移的连续失败次数

#### IpProvider 配置
- `Username` / `Password`: nya.trp.sh 账号凭据
- `DeviceGroupId`: 设备组ID，用于获取IP地址
- `ApiBaseUrl`: nya.trp.sh API基础URL
- `DirectIpApiUrl`: （可选）直接返回IP的API，优先级高于设备组API
  - 响应格式应包含 `current_ip` 字段
  - 示例：`{"current_ip": "1.2.3.4"}`
  - 如果配置此项，系统会优先使用，失败时回退到设备组API

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

**注意**：如果在中国大陆部署，系统默认使用 `https://tg-api.7li7li.com` 作为Telegram API地址，可以正常访问。如果在海外部署，可以修改为官方地址 `https://api.telegram.org`。

### 4. 配置IP提供商

系统支持两种方式获取IP地址：

#### 方式1: 直接IP查询API（推荐）
如果你有一个简单的API可以直接返回IP地址，配置 `DirectIpApiUrl`：
```json
"IpProvider": {
  "DirectIpApiUrl": "https://your-api.com/status"
}
```
API响应格式：
```json
{
  "current_ip": "1.2.3.4"
}
```

#### 方式2: nya.trp.sh 设备组API
配置完整的nya.trp.sh账号信息：
```json
"IpProvider": {
  "Username": "your_username",
  "Password": "your_password",
  "DeviceGroupId": 1,
  "ApiBaseUrl": "https://nya.trp.sh/api/v1"
}
```

**注意**: 如果同时配置了两种方式，系统会优先使用直接IP查询API，失败时自动回退到设备组API。

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

系统采用统一的监控循环，持续执行以下步骤：

### 1. 获取当前IP
- 通过配置的API获取最新IP地址
- 支持直接IP查询API或nya.trp.sh设备组API
- 检测IP变化并记录

### 2. TCPing连通性检测
- 对获取到的IP进行TCPing测试
- 检测指定端口是否可达

### 3. IP可达时的处理
- ✅ 解析主域名当前指向的IP地址
- ✅ 比较域名IP与当前IP是否一致
- ✅ 如果不一致，立即更新A记录为当前IP
- ✅ 如果一致，无需操作

### 4. IP不可达时的处理
- ❌ 累计连续失败次数
- ❌ 达到失败阈值（默认3次）后触发故障转移
- ❌ 将DNS记录切换为CNAME指向备用域名
- ⚠️ **重要**: 切换到CNAME后，系统继续执行步骤1-3，持续监控新IP

### 5. 自动恢复机制
- 无论当前是A记录还是CNAME状态，系统都持续监控
- 一旦检测到新IP可达，自动更新DNS记录
- 无需单独的恢复检测循环，统一在主循环中处理

### 监控周期
- 每个检测周期（默认30秒）执行一次完整流程
- 持续运行，自动适应IP变化

## 通知消息

系统会发送以下类型的Telegram通知：
- � 系统启动通知
- ✅ DNS记录更新通知（A记录变更）
- ⚠️ 故障转移通知（切换到CNAME）
- 📍 IP地址变化通知
- ❌ 错误和异常通知

### 通知示例

**DNS记录更新**:
```
✅ DNS记录已更新

域名: a.com
类型: A记录
IP: 1.2.3.4
原因: IP不一致 (5.6.7.8 → 1.2.3.4)
```

**故障转移**:
```
⚠️ 故障转移已触发

域名: a.com
类型: CNAME
目标: b.com
原因: IP连续3次不可达
状态: 系统将继续监控新IP
```

## 注意事项

1. **Cloudflare配置**
   - 确保API令牌有足够的权限（Zone:DNS:Edit）
   - 建议设置较短的TTL（300秒）以加快DNS传播
   - 主域名应该已存在于Cloudflare中

2. **备用域名**
   - 备用域名应该指向可用的服务
   - 不需要在同一个Cloudflare账号中
   - 系统不会检测备用域名的可用性

3. **IP提供商**
   - 优先配置直接IP查询API以获得更好的性能
   - 确保API返回格式正确（包含 `current_ip` 字段）
   - nya.trp.sh账号需要有访问设备组的权限

4. **监控策略**
   - 系统会持续监控，即使在CNAME状态也会检测新IP
   - 检测间隔不宜过短，避免API限流
   - 失败阈值建议设置为3-5次

5. **网络要求**
   - 运行环境需要能访问Cloudflare API
   - 需要能访问Telegram API
   - 需要能访问配置的IP提供商API
   - TCPing需要能连接到目标端口

## 故障排除

### 常见问题

1. **无法获取IP地址**
   - 检查IP提供商配置是否正确
   - 验证用户名和密码
   - 确认设备组ID是否存在
   - 检查直接IP查询API的URL和响应格式
   - 查看日志中的详细错误信息

2. **无法连接Cloudflare API**
   - 检查API令牌是否正确
   - 确认Zone ID是否匹配
   - 验证网络连接和防火墙设置
   - 确认API令牌权限足够

3. **Telegram通知不工作**
   - 检查Bot Token是否有效
   - 确认Chat ID是否正确
   - 确保机器人有发送消息权限
   - 测试网络是否能访问Telegram API

4. **DNS切换不生效**
   - 检查DNS传播时间（通常需要几分钟）
   - 验证主域名是否正确
   - 确认TTL设置
   - 使用 `nslookup` 或 `dig` 命令验证DNS记录

5. **TCPing检测失败**
   - 确认目标端口是否开放
   - 检查防火墙规则
   - 验证IP地址是否正确
   - 测试网络连通性

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

### 调试技巧

1. **启用详细日志**: 将日志级别设置为 `Debug` 查看详细信息
2. **测试API连接**: 先单独测试IP提供商API是否可访问
3. **验证DNS记录**: 使用 `nslookup` 命令检查当前DNS状态
4. **模拟故障**: 临时关闭目标端口测试故障转移
5. **监控Telegram**: 通过通知消息了解系统运行状态

## 日志管理

系统使用Serilog记录日志，同时输出到控制台和文件。

### 日志文件位置
```
logs/dns-disaster-YYYYMMDD.log
```

### 日志配置
- **滚动策略**: 每天创建新日志文件
- **文件大小限制**: 单个文件最大10MB
- **保留天数**: 保留最近30天的日志
- **文件命名**: `dns-disaster-20260206.log`

### 查看日志

**实时查看日志**（Linux）:
```bash
tail -f logs/dns-disaster-$(date +%Y%m%d).log
```

**查看最近100行**:
```bash
tail -n 100 logs/dns-disaster-$(date +%Y%m%d).log
```

**搜索错误日志**:
```bash
grep "ERR" logs/dns-disaster-*.log
```

**查看特定日期的日志**:
```bash
cat logs/dns-disaster-20260206.log
```

### 日志级别

日志包含以下级别：
- **DBG** (Debug): 详细的调试信息
- **INF** (Information): 一般信息
- **WRN** (Warning): 警告信息
- **ERR** (Error): 错误信息
- **FTL** (Fatal): 致命错误

### 日志格式
```
[2026-02-06 15:30:45 INF] DNS灾难恢复系统启动中...
[2026-02-06 15:30:46 DBG] 检测IP连通性: 1.2.3.4:12345
[2026-02-06 15:30:47 WRN] IP 1.2.3.4 不可达 (失败 1/3)
[2026-02-06 15:30:48 ERR] 发送Telegram通知失败: Connection timeout
```

## 性能优化

- **直接IP查询API**: 相比设备组API，性能提升约7.5倍
- **统一监控循环**: 减少了独立的恢复检测线程，降低资源消耗
- **智能状态管理**: 避免重复的DNS切换操作

## 系统要求

- .NET 8.0 或更高版本
- 网络连接（访问Cloudflare、Telegram、IP提供商API）
- Linux/Windows/macOS 操作系统

## 部署建议

1. **使用systemd服务**（Linux）
   ```bash
   sudo systemctl enable dnsdisaster
   sudo systemctl start dnsdisaster
   ```

2. **使用Docker**
   ```dockerfile
   FROM mcr.microsoft.com/dotnet/runtime:8.0
   COPY ./publish /app
   WORKDIR /app
   ENTRYPOINT ["./DNSDisaster"]
   ```

3. **使用Windows服务**
   - 使用NSSM或类似工具将程序注册为Windows服务

## 许可证

MIT License