@echo off
echo ========================================
echo EyeSharp 项目构建脚本
echo ========================================
echo.

echo [1/4] 还原 NuGet 包...
dotnet restore
if %errorlevel% neq 0 (
    echo 错误：还原包失败
    pause
    exit /b 1
)

echo.
echo [2/4] 构建项目...
dotnet build --configuration Release
if %errorlevel% neq 0 (
    echo 错误：构建失败
    pause
    exit /b 1
)

echo.
echo [3/4] 运行项目...
dotnet run --configuration Release

echo.
echo [4/4] 构建完成！
echo.
echo 如需发布为可执行文件，请运行:
echo   publish.bat
echo.
pause
