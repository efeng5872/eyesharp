@echo off
echo ========================================
echo EyeSharp 项目发布脚本（自包含）
echo ========================================
echo.

echo 正在发布为单文件可执行程序...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

if %errorlevel% neq 0 (
    echo 错误：发布失败
    pause
    exit /b 1
)

echo.
echo ========================================
echo 发布完成！
echo ========================================
echo.
echo 输出位置: bin\Release\net8.0-windows\win-x64\publish\
echo 可执行文件: eyesharp.exe
echo.
echo 您可以将 publish 文件夹中的内容复制到任何 Windows 电脑上运行。
echo.
pause
