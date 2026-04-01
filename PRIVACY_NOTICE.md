# 隐私和安全说明

## 配置文件安全

### 敏感信息保护

本项目的配置文件包含敏感信息，请注意保护：

1. **不要提交实际配置到公开仓库**
   - `appsettings.json` 包含真实的用户名、密码、API密钥
   - 已添加到 `.gitignore`，不会被提交

2. **使用示例配置**
   - `appsettings.example.json` 是示例配置，可以安全分享
   - 所有敏感信息已替换为占位符

3. **文档中的示例**
   - 所有文档使用通用示例（`api.example.com`、`user123`等）
   - 不包含真实的域名、用户名或密码

## 配置文件说明

### 实际配置文件（不要分享）
- `DNSDisaster/appsettings.json` - 包含真实凭据

### 示例配置文件（可以分享）
- `DNSDisaster/appsettings.example.json` - 使用占位符

## 部署建议

### 1. 本地开发
```bash
# 复制示例配置
cp DNSDisaster/appsettings.example.json DNSDisaster/appsettings.json

# 编辑配置，填入真实信息
nano DNSDisaster/appsettings.json
```

### 2. 生产环境
```bash
# 确保配置文件权限正确
chmod 600 appsettings.json

# 只有运行用户可以读取
chown dns-disaster:dns-disaster appsettings.json
```

### 3. 环境变量（推荐）
考虑使用环境变量存储敏感信息：
```bash
export CLOUDFLARE_API_TOKEN="your_token"
export TELEGRAM_BOT_TOKEN="your_bot_token"
```

## 敏感信息清单

配置文件中包含以下敏感信息：

### DNS监控任务
- ✅ API用户名和密码
- ✅ 设备组ID
- ✅ API基础URL

### 套餐监控任务
- ✅ API用户名和密码
- ✅ API基础URL

### Cloudflare配置
- ✅ API Token
- ✅ Zone ID

### Telegram配置
- ✅ Bot Token
- ✅ Chat ID

## 安全最佳实践

1. **定期更换密码**
   - 建议每3-6个月更换一次API密码

2. **使用强密码**
   - 至少12位字符
   - 包含大小写字母、数字和特殊字符

3. **限制API权限**
   - Cloudflare API Token只授予必要的权限（Zone:DNS:Edit）
   - 不要使用全局API Key

4. **监控异常活动**
   - 定期检查日志文件
   - 关注Telegram通知

5. **备份配置**
   - 定期备份配置文件到安全位置
   - 使用加密存储

## 数据隐私

### 本地处理
- 所有套餐信息仅在本地处理
- 不会发送到第三方服务器
- 仅通过Telegram发送通知

### 日志文件
- 日志文件可能包含IP地址和域名信息
- 定期清理旧日志（默认保留30天）
- 确保日志文件权限正确

### 网络通信
- 与API服务器的通信使用HTTPS加密
- 与Telegram的通信使用HTTPS加密
- 与Cloudflare的通信使用HTTPS加密

## 问题报告

如果发现安全问题，请：
1. 不要在公开Issue中讨论
2. 通过私密方式联系维护者
3. 提供详细的问题描述

## 免责声明

- 用户需自行负责保护配置文件安全
- 建议在受信任的服务器上运行
- 定期更新系统和依赖包
