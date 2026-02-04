# 配置更新 - API地址可配置化

## 🔧 更新内容

### 新增配置项
在 `IpProvider` 配置节中新增了 `ApiBaseUrl` 配置项：

```json
{
  "IpProvider": {
    "Username": "goodman",
    "Password": "ELkXV10PxmYHgPzP",
    "DeviceGroupId": 1,
    "ApiBaseUrl": "https://nya.trp.sh/api/v1"
  }
}
```

### 🎯 优化目的

1. **灵活性**: 可以轻松切换到不同的API服务器
2. **可维护性**: API地址变更时只需修改配置文件
3. **测试友好**: 可以配置测试环境的API地址
4. **扩展性**: 为将来支持其他IP提供商做准备

### 📋 配置说明

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| `Username` | string | - | nya.trp.sh 用户名 |
| `Password` | string | - | nya.trp.sh 密码 |
| `DeviceGroupId` | int | 235 | 设备组ID |
| `ApiBaseUrl` | string | `https://nya.trp.sh/api/v1` | API基础地址 |

### 🔍 配置验证

系统会自动验证配置的有效性：
- ✅ 检查所有必填项是否为空
- ✅ 验证 `ApiBaseUrl` 是否为有效的URL格式
- ✅ 确保 `DeviceGroupId` 大于0

### 🌐 支持的API端点

配置的 `ApiBaseUrl` 将用于以下API调用：
- **登录**: `{ApiBaseUrl}/auth/login`
- **设备组**: `{ApiBaseUrl}/user/devicegroup`

### 📝 使用示例

#### 生产环境
```json
{
  "IpProvider": {
    "ApiBaseUrl": "https://nya.trp.sh/api/v1"
  }
}
```

#### 测试环境
```json
{
  "IpProvider": {
    "ApiBaseUrl": "https://test.nya.trp.sh/api/v1"
  }
}
```

#### 自定义服务器
```json
{
  "IpProvider": {
    "ApiBaseUrl": "https://your-custom-server.com/api/v1"
  }
}
```

### 🔄 迁移指南

如果你使用的是旧版本配置，请按以下步骤更新：

1. **备份现有配置**
2. **添加新配置项**:
   ```json
   "ApiBaseUrl": "https://nya.trp.sh/api/v1"
   ```
3. **重新启动系统**

### ✅ 验证更新

更新后，你应该在日志中看到：
```
dbug: 发送登录请求: {"username":"...","password":"..."}
info: 成功获取认证token
info: 找到 X 个设备组
info: 成功获取IP地址: x.x.x.x
```

### 🚀 未来扩展

这个配置化改进为以下功能奠定了基础：
- 支持多个IP提供商
- API版本切换
- 负载均衡和故障转移
- 自定义API实现

现在你的DNS灾难恢复系统更加灵活和可配置了！🎉