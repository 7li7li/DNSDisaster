# 优雅退出指南

## 退出方式

### 方式1：单次 Ctrl+C（推荐）

按一次 `Ctrl+C`，程序会：
1. 停止所有DNS监控服务
2. 停止所有套餐监控服务
3. 等待最多5秒让任务完成
4. 自动退出

```bash
# 按一次 Ctrl+C
^C
[2026-04-07 11:33:52 INF] 收到退出信号，正在停止所有服务...
[2026-04-07 11:33:53 INF] [Subscription-User1] 套餐监控服务收到取消信号
[2026-04-07 11:33:53 INF] [DNSDisaster] DNS监控服务已停止
[2026-04-07 11:33:53 INF] 所有任务已取消
[2026-04-07 11:33:53 INF] DNS灾难恢复系统已停止
```

### 方式2：双击 Ctrl+C（强制退出）

如果第一次按 `Ctrl+C` 后程序没有在5秒内退出，可以再按一次强制退出：

```bash
# 第一次
^C
[2026-04-07 11:33:52 INF] 收到退出信号，正在停止所有服务...
[2026-04-07 11:33:52 INF] 提示: 再次按 Ctrl+C 可强制退出

# 第二次（如果需要）
^C
[2026-04-07 11:33:55 WRN] 强制退出程序
```

### 方式3：使用 systemctl（服务模式）

```bash
# 停止服务
sudo systemctl stop dns-disaster

# 查看状态
sudo systemctl status dns-disaster
```

## 退出流程

### 正常退出流程

```
用户按 Ctrl+C
    ↓
捕获退出信号
    ↓
停止所有DNS监控服务
    ↓
停止所有套餐监控服务
    ↓
等待任务完成（最多5秒）
    ↓
清理资源
    ↓
程序退出
```

### 超时处理

如果任务在5秒内没有完成：
- 程序会自动强制退出
- 日志会显示 "等待任务完成超时，强制退出"

### 强制退出

如果需要立即退出：
- 第一次 `Ctrl+C`：优雅退出（等待5秒）
- 第二次 `Ctrl+C`：立即强制退出

## 退出时的日志

### 正常退出
```
[INF] 收到退出信号，正在停止所有服务...
[INF] [TaskName] 套餐监控服务收到取消信号
[INF] [TaskName] DNS监控服务已停止
[INF] 所有任务已取消
[INF] DNS灾难恢复系统已停止
```

### 超时退出
```
[INF] 收到退出信号，正在停止所有服务...
[WRN] 等待任务完成超时，强制退出
[INF] DNS灾难恢复系统已停止
```

### 强制退出
```
[INF] 收到退出信号，正在停止所有服务...
[INF] 提示: 再次按 Ctrl+C 可强制退出
[WRN] 强制退出程序
```

## 故障排查

### 问题1：按 Ctrl+C 后卡住不动

**原因**：某个任务可能没有正确响应取消信号

**解决方案**：
1. 等待5秒，程序会自动超时退出
2. 或者再按一次 `Ctrl+C` 强制退出

### 问题2：程序完全无响应

**解决方案**：使用 `kill` 命令强制终止

```bash
# 查找进程ID
ps aux | grep DNSDisaster

# 强制杀死进程
kill -9 <PID>

# 或者使用 pkill
pkill -9 -f DNSDisaster
```

### 问题3：systemd 服务停不下来

```bash
# 强制停止
sudo systemctl kill dns-disaster

# 重置失败状态
sudo systemctl reset-failed dns-disaster

# 重新启动
sudo systemctl start dns-disaster
```

## 最佳实践

### 1. 正常停止
```bash
# 前台运行时
Ctrl+C

# 服务模式
sudo systemctl stop dns-disaster
```

### 2. 检查是否已停止
```bash
# 检查进程
ps aux | grep DNSDisaster

# 检查服务状态
sudo systemctl status dns-disaster
```

### 3. 查看退出日志
```bash
# 查看最近的日志
tail -n 50 logs/dns-disaster-$(date +%Y%m%d).log

# 或者使用 journalctl（服务模式）
sudo journalctl -u dns-disaster -n 50
```

## 退出超时配置

当前超时设置：
- **优雅退出等待时间**：5秒
- **第二次 Ctrl+C**：立即退出

如果需要修改超时时间，可以在 `Program.cs` 中调整：

```csharp
// 修改这里的超时时间（当前是5秒）
using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
```

## 注意事项

1. **数据安全**：
   - 优雅退出会等待当前操作完成
   - 强制退出可能导致正在进行的操作中断

2. **日志完整性**：
   - 优雅退出会确保日志正确写入
   - 强制退出可能丢失最后的日志

3. **资源清理**：
   - 优雅退出会正确释放所有资源
   - 强制退出可能留下未清理的资源

4. **推荐做法**：
   - 优先使用单次 `Ctrl+C`
   - 等待5秒让程序自动退出
   - 只在必要时使用强制退出

## 相关文档

- [DEPLOY.md](DNSDisaster/DEPLOY.md) - 部署指南
- [LOGGING.md](DNSDisaster/LOGGING.md) - 日志管理
- [README.md](README.md) - 主文档
