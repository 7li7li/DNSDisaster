#!/bin/bash

echo "Deploying DNS Disaster Recovery System..."

# 构建发布版本
echo "Building release version..."
dotnet publish -c Release -r linux-x64 --self-contained -o ./publish

# 创建部署目录
sudo mkdir -p /opt/dns-disaster

# 复制文件
echo "Copying files..."
sudo cp -r ./publish/* /opt/dns-disaster/
sudo cp appsettings.json /opt/dns-disaster/ 2>/dev/null || echo "Warning: appsettings.json not found, please configure it manually"
sudo cp dns-disaster.service /etc/systemd/system/

# 设置权限
sudo chmod +x /opt/dns-disaster/DNSDisaster
sudo chown -R root:root /opt/dns-disaster

# 重新加载systemd并启用服务
echo "Setting up systemd service..."
sudo systemctl daemon-reload
sudo systemctl enable dns-disaster.service

echo "Deployment completed!"
echo "To start the service: sudo systemctl start dns-disaster"
echo "To check status: sudo systemctl status dns-disaster"
echo "To view logs: sudo journalctl -u dns-disaster -f"