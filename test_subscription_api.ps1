# 套餐监控 API 测试脚本 (PowerShell)
# 用于验证 API 接口是否正常工作

# 配置
$ApiBaseUrl = "https://api.example.com/v1"
$Username = "your_username"
$Password = "your_password"

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "套餐监控 API 测试" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

# 步骤1: 登录获取 token
Write-Host "步骤1: 登录获取 token..." -ForegroundColor Yellow

$loginBody = @{
    username = $Username
    password = $Password
} | ConvertTo-Json

try {
    $loginResponse = Invoke-RestMethod -Uri "$ApiBaseUrl/auth/login" `
        -Method Post `
        -ContentType "application/json" `
        -Body $loginBody

    Write-Host "登录响应: $($loginResponse | ConvertTo-Json -Depth 10)" -ForegroundColor Gray
    Write-Host ""

    $token = $loginResponse.data

    if ([string]::IsNullOrEmpty($token)) {
        Write-Host "❌ 登录失败，无法获取 token" -ForegroundColor Red
        exit 1
    }

    Write-Host "✅ 成功获取 token: $($token.Substring(0, [Math]::Min(20, $token.Length)))..." -ForegroundColor Green
    Write-Host ""

    # 步骤2: 获取用户信息
    Write-Host "步骤2: 获取用户信息..." -ForegroundColor Yellow

    $headers = @{
        "Authorization" = $token
    }

    $userInfoResponse = Invoke-RestMethod -Uri "$ApiBaseUrl/user/info" `
        -Method Get `
        -Headers $headers

    Write-Host "用户信息响应:" -ForegroundColor Gray
    Write-Host ($userInfoResponse | ConvertTo-Json -Depth 10) -ForegroundColor Gray
    Write-Host ""

    # 步骤3: 解析关键信息
    Write-Host "=========================================" -ForegroundColor Cyan
    Write-Host "套餐状态摘要" -ForegroundColor Cyan
    Write-Host "=========================================" -ForegroundColor Cyan

    $data = $userInfoResponse.data
    
    Write-Host "到期时间: $($data.expire_time)"
    Write-Host "已用流量: $($data.used_traffic) GB"
    Write-Host "总流量: $($data.total_traffic) GB"
    Write-Host "余额: $($data.balance) 元"
    Write-Host "续费价格: $($data.renewal_price) 元"
    Write-Host ""

    # 步骤4: 检查警告条件
    Write-Host "=========================================" -ForegroundColor Cyan
    Write-Host "警告检查" -ForegroundColor Cyan
    Write-Host "=========================================" -ForegroundColor Cyan

    # 检查流量使用率
    if ($data.total_traffic -gt 0) {
        $usagePercent = ($data.used_traffic / $data.total_traffic) * 100
        Write-Host "流量使用率: $([Math]::Round($usagePercent, 1))%"
        
        if ($usagePercent -ge 90) {
            Write-Host "⚠️  警告: 流量使用超过 90%" -ForegroundColor Red
        } elseif ($usagePercent -ge 80) {
            Write-Host "⚠️  提示: 流量使用超过 80%" -ForegroundColor Yellow
        } else {
            Write-Host "✅ 流量使用正常" -ForegroundColor Green
        }
    } else {
        Write-Host "⚠️  无法计算流量使用率" -ForegroundColor Yellow
    }
    Write-Host ""

    # 检查余额
    if ($data.balance -lt $data.renewal_price) {
        Write-Host "⚠️  警告: 余额不足以续费" -ForegroundColor Red
        Write-Host "   当前余额: $($data.balance) 元"
        Write-Host "   续费需要: $($data.renewal_price) 元"
        Write-Host "   差额: $($data.renewal_price - $data.balance) 元"
    } else {
        Write-Host "✅ 余额充足" -ForegroundColor Green
    }
    Write-Host ""

    # 检查到期时间
    if ($data.expire_time) {
        try {
            $expireDate = [DateTime]::Parse($data.expire_time)
            $daysLeft = ($expireDate - (Get-Date)).TotalDays
            
            Write-Host "剩余天数: $([Math]::Round($daysLeft, 1)) 天"
            
            if ($daysLeft -le 7) {
                Write-Host "⚠️  警告: 套餐将在 7 天内到期" -ForegroundColor Red
            } elseif ($daysLeft -le 14) {
                Write-Host "⚠️  提示: 套餐将在 14 天内到期" -ForegroundColor Yellow
            } else {
                Write-Host "✅ 套餐有效期充足" -ForegroundColor Green
            }
        } catch {
            Write-Host "⚠️  无法解析到期时间" -ForegroundColor Yellow
        }
    } else {
        Write-Host "⚠️  未获取到到期时间" -ForegroundColor Yellow
    }
    Write-Host ""

} catch {
    Write-Host "❌ 错误: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "详细信息:" -ForegroundColor Gray
    Write-Host $_.Exception -ForegroundColor Gray
}

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "测试完成" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
