#!/bin/bash

# 套餐监控 API 测试脚本
# 用于验证 API 接口是否正常工作

# 配置
API_BASE_URL="https://api.example.com/v1"
USERNAME="your_username"
PASSWORD="your_password"

echo "========================================="
echo "套餐监控 API 测试"
echo "========================================="
echo ""

# 步骤1: 登录获取 token
echo "步骤1: 登录获取 token..."
LOGIN_RESPONSE=$(curl -s -X POST "${API_BASE_URL}/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"${USERNAME}\",\"password\":\"${PASSWORD}\"}")

echo "登录响应: ${LOGIN_RESPONSE}"
echo ""

# 提取 token
TOKEN=$(echo "${LOGIN_RESPONSE}" | grep -o '"data":"[^"]*"' | cut -d'"' -f4)

if [ -z "$TOKEN" ]; then
    echo "❌ 登录失败，无法获取 token"
    exit 1
fi

echo "✅ 成功获取 token: ${TOKEN:0:20}..."
echo ""

# 步骤2: 获取用户信息
echo "步骤2: 获取用户信息..."
USER_INFO_RESPONSE=$(curl -s -X GET "${API_BASE_URL}/user/info" \
  -H "Authorization: ${TOKEN}")

echo "用户信息响应:"
echo "${USER_INFO_RESPONSE}" | python3 -m json.tool 2>/dev/null || echo "${USER_INFO_RESPONSE}"
echo ""

# 步骤3: 解析关键信息
echo "========================================="
echo "套餐状态摘要"
echo "========================================="

# 提取字段（简单方式，生产环境建议使用 jq）
EXPIRE_TIME=$(echo "${USER_INFO_RESPONSE}" | grep -o '"expire_time":"[^"]*"' | cut -d'"' -f4)
USED_TRAFFIC=$(echo "${USER_INFO_RESPONSE}" | grep -o '"used_traffic":[0-9.]*' | cut -d':' -f2)
TOTAL_TRAFFIC=$(echo "${USER_INFO_RESPONSE}" | grep -o '"total_traffic":[0-9.]*' | cut -d':' -f2)
BALANCE=$(echo "${USER_INFO_RESPONSE}" | grep -o '"balance":[0-9.]*' | cut -d':' -f2)
RENEWAL_PRICE=$(echo "${USER_INFO_RESPONSE}" | grep -o '"renewal_price":[0-9.]*' | cut -d':' -f2)

echo "到期时间: ${EXPIRE_TIME:-未知}"
echo "已用流量: ${USED_TRAFFIC:-0} GB"
echo "总流量: ${TOTAL_TRAFFIC:-0} GB"
echo "余额: ${BALANCE:-0} 元"
echo "续费价格: ${RENEWAL_PRICE:-0} 元"
echo ""

# 步骤4: 检查警告条件
echo "========================================="
echo "警告检查"
echo "========================================="

# 检查流量使用率
if [ -n "$USED_TRAFFIC" ] && [ -n "$TOTAL_TRAFFIC" ] && [ "$TOTAL_TRAFFIC" != "0" ]; then
    USAGE_PERCENT=$(echo "scale=1; ($USED_TRAFFIC / $TOTAL_TRAFFIC) * 100" | bc)
    echo "流量使用率: ${USAGE_PERCENT}%"
    
    if (( $(echo "$USAGE_PERCENT >= 90" | bc -l) )); then
        echo "⚠️  警告: 流量使用超过 90%"
    elif (( $(echo "$USAGE_PERCENT >= 80" | bc -l) )); then
        echo "⚠️  提示: 流量使用超过 80%"
    else
        echo "✅ 流量使用正常"
    fi
else
    echo "⚠️  无法计算流量使用率"
fi
echo ""

# 检查余额
if [ -n "$BALANCE" ] && [ -n "$RENEWAL_PRICE" ]; then
    if (( $(echo "$BALANCE < $RENEWAL_PRICE" | bc -l) )); then
        echo "⚠️  警告: 余额不足以续费"
        echo "   当前余额: ${BALANCE} 元"
        echo "   续费需要: ${RENEWAL_PRICE} 元"
        echo "   差额: $(echo "$RENEWAL_PRICE - $BALANCE" | bc) 元"
    else
        echo "✅ 余额充足"
    fi
else
    echo "⚠️  无法检查余额状态"
fi
echo ""

# 检查到期时间
if [ -n "$EXPIRE_TIME" ]; then
    EXPIRE_TIMESTAMP=$(date -d "$EXPIRE_TIME" +%s 2>/dev/null)
    CURRENT_TIMESTAMP=$(date +%s)
    
    if [ -n "$EXPIRE_TIMESTAMP" ]; then
        DAYS_LEFT=$(echo "scale=1; ($EXPIRE_TIMESTAMP - $CURRENT_TIMESTAMP) / 86400" | bc)
        echo "剩余天数: ${DAYS_LEFT} 天"
        
        if (( $(echo "$DAYS_LEFT <= 7" | bc -l) )); then
            echo "⚠️  警告: 套餐将在 7 天内到期"
        elif (( $(echo "$DAYS_LEFT <= 14" | bc -l) )); then
            echo "⚠️  提示: 套餐将在 14 天内到期"
        else
            echo "✅ 套餐有效期充足"
        fi
    else
        echo "⚠️  无法解析到期时间"
    fi
else
    echo "⚠️  未获取到到期时间"
fi
echo ""

echo "========================================="
echo "测试完成"
echo "========================================="
