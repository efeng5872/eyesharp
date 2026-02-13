@echo off
echo ========================================
echo EyeSharp 项目测试脚本
echo ========================================
echo.

echo [测试 1/5] 检查项目文件...
if not exist "eyesharp.csproj" (
    echo 错误：未找到项目文件
    pause
    exit /b 1
)
echo ✓ 项目文件存在

echo.
echo [测试 2/5] 还原 NuGet 包...
dotnet restore
if %errorlevel% neq 0 (
    echo 错误：还原包失败
    pause
    exit /b 1
)
echo ✓ NuGet 包还原成功

echo.
echo [测试 3/5] 构建项目...
dotnet build --configuration Debug
if %errorlevel% neq 0 (
    echo 错误：构建失败
    pause
    exit /b 1
)
echo ✓ 项目构建成功

echo.
echo [测试 4/5] 检查输出目录...
if not exist "bin\Debug\net8.0-windows\eyesharp.exe" (
    echo 错误：未找到可执行文件
    pause
    exit /b 1
)
echo ✓ 可执行文件生成成功

echo.
echo [测试 5/5] 运行程序...
echo 提示：请测试以下功能：
echo   - 首次启动应显示密码设置对话框
echo   - 密码设置后进入主窗口
echo   - 配置文件损坏应显示恢复对话框
echo.
pause

dotnet run --configuration Debug

echo.
echo ========================================
echo 测试完成！
echo ========================================
pause
