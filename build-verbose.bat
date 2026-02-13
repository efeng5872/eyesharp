@echo off
echo ====================================
echo EyeSharp 项目构建脚本
echo ====================================
echo.

cd /d "%~dp0"

echo [1/2] 还原 NuGet 包...
dotnet restore
if %errorlevel% neq 0 (
    echo.
    echo 错误: NuGet 包还原失败！
    pause
    exit /b 1
)

echo.
echo [2/2] 构建项目...
dotnet build --configuration Debug
if %errorlevel% neq 0 (
    echo.
    echo 错误: 项目构建失败！
    pause
    exit /b 1
)

echo.
echo ====================================
echo 构建成功！
echo ====================================
echo.
echo 运行程序: dotnet run
echo 或直接运行: bin\Debug\net8.0-windows\eyesharp.exe
echo.
pause
