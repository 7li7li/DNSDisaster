# 测试配置说明

## Visual Studio 调试问题已解决

现在你可以在Visual Studio中正常调试程序了。问题已通过以下方式解决：

1. **配置文件自动复制**: 在 `.csproj` 文件中添加了配置，使 `appsettings.json` 自动复制到输出目录
2. **启动设置**: 创建了 `Properties/launchSettings.json` 确保正确的工作目录
3. **Telegram Chat ID 支持**: 修复了Chat ID格式问题，现在支持数字ID和@username格式

## 当前状态

程序现在可以正常启动，会显示以下信息：
- ✅ DNS灾难恢复系统启动
- ✅ 开始DNS监控服务
- ⚠️ Telegram通知失败（因为使用示例配置，这是正常的）
- ✅ 获取主域名A记录IP
- ✅ TCP连接检测正常工作

## 下一步配置

要让系统完全工作，你需要：

1. **配置Cloudflare**:
   ```json
   "Cloudflare": {
     "ApiToken": "你的真实API Token",
     "ZoneId": "你的真实Zone ID", 
     "RecordName": "你的域名"
   }
   ```

2. **配置Telegram**:
   ```json
   "Telegram": {
     "BotToken": "你的真实Bot Token",
     "ChatId": "你的真实Chat ID（数字格式）"
   }
   ```

3. **配置监控目标**:
   ```json
   "DNSDisaster": {
     "PrimaryDomain": "你的主域名",
     "PrimaryPort": 你的端口,
     "BackupDomain": "你的备用域名"
   }
   ```

## 测试建议

1. 先用真实的Telegram配置测试通知功能
2. 再配置Cloudflare进行DNS切换测试
3. 最后进行完整的故障转移测试

现在你可以在Visual Studio中正常调试和开发了！