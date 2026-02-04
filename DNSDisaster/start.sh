#!/bin/bash

echo "Starting DNS Disaster Recovery System..."
echo

# 检查配置文件是否存在
if [ ! -f "appsettings.json" ]; then
    echo "Error: appsettings.json not found!"
    echo "Please copy appsettings.example.json to appsettings.json and configure it."
    exit 1
fi

# 启动应用程序
dotnet run