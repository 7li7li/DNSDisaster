@echo off
echo Starting DNS Disaster Recovery System...
echo.

REM 检查配置文件是否存在
if not exist "appsettings.json" (
    echo Error: appsettings.json not found!
    echo Please copy appsettings.example.json to appsettings.json and configure it.
    pause
    exit /b 1
)

REM 启动应用程序
dotnet run

pause