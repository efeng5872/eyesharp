# EyeSharp 护眼助手

一款基于 WPF 开发的定时提醒休息应用程序，帮助保护您的眼睛健康。

## 项目说明

本项目严格按照「需求分析→设计→测试规划→开发→测试→Bug修复→部署」的流程进行开发。

### 文档位置

- 需求文档：`docs/需求文档.md`
- 设计文档：`docs/设计文档.md`
- 测试文档：`docs/测试文档.md`
- 问题与解答：`tt.txt`

## 技术栈

- .NET 8 LTS
- WPF (C# + XAML)
- CommunityToolkit.Mvvm 8.2+ (MVVM 框架)
- H.NotifyIcon.Wpf 2.0+ (系统托盘)
- NLog 5.3+ (日志框架)

## 项目结构

```
eyesharp/
├── Models/           # 数据模型
├── ViewModels/       # 视图模型
├── Views/            # 视图（XAML）
├── Services/         # 业务服务
├── Helpers/          # 辅助类
├── Converters/       # 值转换器
├── Resources/        # 资源文件
├── Config/           # 配置文件
├── docs/             # 项目文档
├── App.xaml          # 应用程序入口
├── App.xaml.cs
├── MainWindow.xaml   # 主窗口
├── MainWindow.xaml.cs
├── NLog.config       # 日志配置
└── eyesharp.csproj   # 项目文件
```

## 构建要求

### 必需软件

1. **.NET 8 SDK**
   - 下载地址：https://dotnet.microsoft.com/download/dotnet/8.0
   - 安装后验证：`dotnet --version`

2. **Visual Studio 2022** (推荐)
   - 社区版免费：https://visualstudio.microsoft.com/downloads/
   - 需要安装 ".NET 桌面开发" 工作负载

或使用 **Visual Studio Code**
   - 安装 C# Dev Kit 扩展
   - 安装 .NET Install Tool 扩展

## 构建步骤

### 方法一：使用 Visual Studio

1. 打开 Visual Studio
2. 选择 "打开项目或解决方案"
3. 选择 `eyesharp.csproj` 文件
4. 按 `F5` 或点击 "启动" 按钮运行

### 方法二：使用命令行

```bash
# 1. 进入项目目录
cd D:\java\ClaudeCode\eyesharp

# 2. 还原依赖包
dotnet restore

# 3. 构建项目
dotnet build

# 4. 运行项目
dotnet run
```

## 发布

### 自包含发布（推荐）

将 .NET 运行时打包到 exe 中，无需用户安装 .NET：

```bash
# 发布为单文件 exe
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# 输出位置：bin/Release/net8.0-windows/win-x64/publish/
```

### 依赖框架发布

需要用户已安装 .NET 8 运行时：

```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

## 开发进度

- [x] Phase 1: 项目搭建 + 基础架构
- [ ] Phase 2: 配置管理 + 日志系统
- [ ] Phase 3: 定时器模块 + 倒计时显示
- [ ] Phase 4: 密码安全模块
- [ ] Phase 5: 休息窗口（黑屏模式）
- [ ] Phase 6: 图片轮播功能
- [ ] Phase 7: 系统托盘
- [ ] Phase 8: 单例模式 + 开机自启动
- [ ] Phase 9: 集成测试 + Bug 修复

## 主要功能

### 已实现

- [x] 基础 MVVM 架构
- [x] 配置文件读写服务
- [x] 密码哈希服务（PBKDF2-HMAC-SHA256）
- [x] 日志服务（NLog）
- [x] 主窗口 UI 布局
- [x] 依赖注入容器
- [x] 单实例检测

### 待实现

- [ ] 定时器服务
- [ ] 休息窗口
- [ ] 系统托盘集成
- [ ] 图片轮播
- [ ] 开机自启动
- [ ] 密码修改对话框
- [ ] 其他对话框

## 注意事项

1. 首次运行需要设置密码（强制模式）
2. 配置文件默认保存在 `config.json`
3. 日志文件保存在 `logs/` 目录
4. 休息窗口模式：黑屏或图片轮播
5. 时间格式统一使用北京时间（UTC+8）

## 许可证

Copyright © 2025 eyesharp
