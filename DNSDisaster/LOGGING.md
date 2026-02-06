# 日志管理指南

## 日志配置

DNS灾难恢复系统使用 **Serilog** 作为日志框架，提供强大的日志记录和管理功能。

### 日志输出

系统同时输出日志到两个位置：

1. **控制台** - 用于实时查看（前台运行时）
2. **文件** - 用于持久化存储和后续分析

### 日志文件

**位置**: `logs/dns-disaster-YYYYMMDD.log`

**示例**:
```
logs/dns-disaster-20260206.log
logs/dns-disaster-20260207.log
logs/dns-disaster-20260208.log
```

### 日志策略

| 配置项 | 值 | 说明 |
|--------|-----|------|
| 滚动策略 | 每天 | 每天创建新的日志文件 |
| 文件大小限制 | 10MB | 单个文件最大10MB |
| 大小滚动 | 启用 | 超过10MB自动创建新文件 |
| 保留天数 | 30天 | 自动删除30天前的日志 |
| 时间格式 | yyyy-MM-dd HH:mm:ss | 24小时制 |

## 日志级别

系统使用以下日志级别（从低到高）：

| 级别 | 缩写 | 用途 | 示例 |
|------|------|------|------|
| Debug | DBG | 详细调试信息 | 检测IP连通性、DNS查询结果 |
| Information | INF | 一般信息 | 系统启动、服务状态变化 |
| Warning | WRN | 警告信息 | 连接失败、IP不可达 |
| Error | ERR | 错误信息 | API调用失败、配置错误 |
| Fatal | FTL | 致命错误 | 系统崩溃、无法启动 |

## 日志格式

```
[时间戳 级别] 消息内容
[异常堆栈]
```

**示例**:
```
[2026-02-06 15:30:45 INF] DNS灾难恢复系统启动中...
[2026-02-06 15:30:46 DBG] 检测IP连通性: 1.2.3.4:12345
[2026-02-06 15:30:47 INF] ✅ IP 1.2.3.4 可达
[2026-02-06 15:30:48 WRN] IP 1.2.3.4 不可达 (失败 1/3)
[2026-02-06 15:30:49 ERR] 发送Telegram通知失败: Connection timeout
System.Net.Http.HttpRequestException: Connection timeout
   at System.Net.Http.HttpClient.SendAsync(...)
```

## 查看日志

### Linux命令

**实时查看今天的日志**:
```bash
tail -f logs/dns-disaster-$(date +%Y%m%d).log
```

**查看最近100行**:
```bash
tail -n 100 logs/dns-disaster-$(date +%Y%m%d).log
```

**查看所有日志文件**:
```bash
ls -lh logs/
```

**查看特定日期的日志**:
```bash
cat logs/dns-disaster-20260206.log
```

**搜索特定内容**:
```bash
# 搜索错误
grep "ERR" logs/dns-disaster-*.log

# 搜索警告
grep "WRN" logs/dns-disaster-*.log

# 搜索特定IP
grep "1.2.3.4" logs/dns-disaster-*.log

# 搜索故障转移
grep "故障转移" logs/dns-disaster-*.log
```

**统计日志**:
```bash
# 统计错误数量
grep -c "ERR" logs/dns-disaster-$(date +%Y%m%d).log

# 统计今天的日志行数
wc -l logs/dns-disaster-$(date +%Y%m%d).log

# 查看日志文件大小
du -h logs/dns-disaster-*.log
```

### 使用 less 查看

```bash
# 分页查看日志
less logs/dns-disaster-$(date +%Y%m%d).log

# 在less中的操作：
# - 空格键：下一页
# - b：上一页
# - /关键词：搜索
# - n：下一个搜索结果
# - q：退出
```

### 使用 journalctl（systemd服务）

如果作为systemd服务运行，也可以使用journalctl：

```bash
# 实时查看
sudo journalctl -u dns-disaster -f

# 查看最近100行
sudo journalctl -u dns-disaster -n 100

# 查看今天的日志
sudo journalctl -u dns-disaster --since today

# 查看昨天的日志
sudo journalctl -u dns-disaster --since yesterday --until today

# 查看特定时间范围
sudo journalctl -u dns-disaster --since "2026-02-06 00:00:00" --until "2026-02-06 23:59:59"
```

## 日志分析

### 常见日志模式

**系统启动**:
```
[2026-02-06 15:30:45 INF] DNS灾难恢复系统启动中...
[2026-02-06 15:30:45 INF] Telegram Bot 初始化完成，使用API地址: https://tg-api.7li7li.com
[2026-02-06 15:30:46 INF] 开始DNS监控服务...
```

**正常监控**:
```
[2026-02-06 15:30:47 DBG] 检测IP连通性: 1.2.3.4:12345
[2026-02-06 15:30:48 DBG] ✅ IP 1.2.3.4 可达
```

**IP变化**:
```
[2026-02-06 15:31:00 INF] 检测到IP变化: 1.2.3.4 → 5.6.7.8
[2026-02-06 15:31:01 INF] [2026-02-06 15:31:01] 首次检测DNS一致性: IP=5.6.7.8
```

**故障转移**:
```
[2026-02-06 15:32:00 WRN] ❌ IP 1.2.3.4 不可达 (失败 1/3)
[2026-02-06 15:32:30 WRN] ❌ IP 1.2.3.4 不可达 (失败 2/3)
[2026-02-06 15:33:00 WRN] ❌ IP 1.2.3.4 不可达 (失败 3/3)
[2026-02-06 15:33:01 WRN] ⚠️ 触发故障转移: 切换到CNAME备用域名 backup.example.com
[2026-02-06 15:33:02 INF] 成功切换到CNAME: backup.example.com
```

**DNS更新**:
```
[2026-02-06 15:34:00 INF] 准备切换到A记录: IP=5.6.7.8, 原因=IP不一致 (1.2.3.4 → 5.6.7.8)
[2026-02-06 15:34:01 INF] 成功切换到A记录: 5.6.7.8
[2026-02-06 15:34:02 INF] Telegram通知发送成功: DNS记录已更新
```

## 日志维护

### 自动清理

系统会自动：
- 保留最近30天的日志文件
- 删除超过30天的旧日志
- 当文件超过10MB时自动滚动

### 手动清理

**删除7天前的日志**:
```bash
find logs/ -name "dns-disaster-*.log" -mtime +7 -delete
```

**压缩旧日志**:
```bash
# 压缩7天前的日志
find logs/ -name "dns-disaster-*.log" -mtime +7 -exec gzip {} \;

# 查看压缩的日志
zcat logs/dns-disaster-20260101.log.gz | less
```

**备份日志**:
```bash
# 备份到其他目录
tar -czf dns-disaster-logs-$(date +%Y%m).tar.gz logs/

# 备份到远程服务器
rsync -avz logs/ user@backup-server:/backup/dns-disaster/logs/
```

### 日志轮转（可选）

如果需要更复杂的日志管理，可以使用logrotate：

创建 `/etc/logrotate.d/dns-disaster`:
```
/opt/dns-disaster/logs/dns-disaster-*.log {
    daily
    rotate 30
    compress
    delaycompress
    missingok
    notifempty
    create 0644 root root
}
```

## 故障排查

### 日志文件不存在

**问题**: 没有生成日志文件

**检查**:
```bash
# 检查logs目录是否存在
ls -la /opt/dns-disaster/

# 检查权限
ls -la /opt/dns-disaster/logs/

# 手动创建目录
mkdir -p /opt/dns-disaster/logs
chmod 755 /opt/dns-disaster/logs
```

### 日志文件过大

**问题**: 日志文件占用太多空间

**解决**:
```bash
# 查看日志目录大小
du -sh /opt/dns-disaster/logs/

# 清理旧日志
find /opt/dns-disaster/logs/ -name "*.log" -mtime +7 -delete

# 压缩旧日志
find /opt/dns-disaster/logs/ -name "*.log" -mtime +7 -exec gzip {} \;
```

### 权限问题

**问题**: 无法写入日志文件

**解决**:
```bash
# 修改目录权限
sudo chown -R dns-disaster:dns-disaster /opt/dns-disaster/logs/
sudo chmod -R 755 /opt/dns-disaster/logs/
```

## 监控建议

### 定期检查

建议每天检查日志中的错误和警告：

```bash
# 创建检查脚本
cat > /usr/local/bin/check-dns-disaster-logs.sh << 'EOF'
#!/bin/bash
LOG_FILE="/opt/dns-disaster/logs/dns-disaster-$(date +%Y%m%d).log"

if [ -f "$LOG_FILE" ]; then
    ERROR_COUNT=$(grep -c "ERR" "$LOG_FILE")
    WARN_COUNT=$(grep -c "WRN" "$LOG_FILE")
    
    echo "今日日志统计:"
    echo "错误: $ERROR_COUNT"
    echo "警告: $WARN_COUNT"
    
    if [ $ERROR_COUNT -gt 0 ]; then
        echo ""
        echo "最近的错误:"
        grep "ERR" "$LOG_FILE" | tail -n 5
    fi
fi
EOF

chmod +x /usr/local/bin/check-dns-disaster-logs.sh

# 添加到crontab每天早上9点执行
echo "0 9 * * * /usr/local/bin/check-dns-disaster-logs.sh" | crontab -
```

### 告警设置

可以设置日志告警，当出现错误时发送通知（系统已通过Telegram发送关键事件通知）。

## 最佳实践

1. **定期查看日志** - 每天检查一次日志，了解系统运行状态
2. **保留足够的日志** - 至少保留30天，便于问题追溯
3. **监控日志大小** - 避免日志占用过多磁盘空间
4. **备份重要日志** - 定期备份日志到其他位置
5. **使用日志分析** - 通过日志分析发现潜在问题
6. **调整日志级别** - 生产环境使用Information级别，调试时使用Debug级别

## 日志级别调整

如果需要更详细的日志，可以修改 `Program.cs` 中的日志配置：

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()  // 改为 .Verbose() 获取更详细的日志
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("System", LogEventLevel.Information)
    // ... 其他配置
```

然后重新编译和部署。
