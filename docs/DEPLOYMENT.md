# EyeSharp 护眼助手 - 部署手册

## 一、环境要求

### 1.1 系统要求
| 项目 | 要求 |
|------|------|
| 操作系统 | Windows 10 版本 1809 或更高版本 / Windows 11 |
| 运行时 | .NET 8.0 Windows 桌面运行时（如使用非自包含版本） |
| 磁盘空间 | 至少 200MB 可用空间 |
| 内存 | 至少 512MB RAM |

### 1.2 运行时可选方案
- **方案A（推荐）**: 使用自包含版本（已包含运行时，无需额外安装）
- **方案B**: 安装 [.NET 8.0 Windows 桌面运行时](https://dotnet.microsoft.com/download/dotnet/8.0)

## 二、部署步骤

### 2.1 单文件部署（推荐）

#### 步骤1：准备发布文件
```bash
dotnet publish --configuration Release --runtime win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  --output ./publish
```

#### 步骤2：分发文件
将以下文件复制到目标目录：
```
publish/
├── eyesharp.exe      # 主程序（单文件，约175MB）
├── eyesharp.pdb      # 调试符号（可选）
└── NLog.config       # 日志配置文件（可选，使用默认配置）
```

#### 步骤3：首次运行
1. 双击 `eyesharp.exe` 启动程序
2. 首次启动会提示设置密码
3. 设置完成后进入主界面，开始护眼提醒

### 2.2 框架依赖部署（体积小）

#### 步骤1：准备发布文件
```bash
dotnet publish --configuration Release --runtime win-x64 --self-contained false ^
  -p:PublishSingleFile=true ^
  --output ./publish-fdd
```

#### 步骤2：安装运行时
确保目标机器已安装 [.NET 8.0 Windows 桌面运行时](https://dotnet.microsoft.com/download/dotnet/8.0)

#### 步骤3：分发与运行
同单文件部署步骤2-3

## 三、配置说明

### 3.1 配置文件
程序首次运行时会自动创建 `config.json` 文件：
```json
{
  "restIntervalMinutes": 30,    // 休息间隔（分钟）
  "restDurationSeconds": 20,    // 休息时长（秒）
  "isForcedMode": true,         // 是否强制模式
  "restWindowMode": "black",    // 休息窗口模式：black/image
  "customImagePath": "",        // 自定义图片路径
  "passwordHash": "...",        // 密码哈希（自动加密）
  "autoStart": false,           // 开机自启动
  "logLevel": "INFO"            // 日志级别
}
```

### 3.2 日志文件
- 位置：`logs/` 目录（与程序同级）
- 命名格式：`eyesharp-YYYYMMDD.log`
- 保留策略：自动保留最近30天的日志

## 四、验证标准

### 4.1 功能验证清单
- [ ] 程序正常启动，无报错
- [ ] 首次启动显示密码设置对话框
- [ ] 倒计时正常显示（格式：MM:SS）
- [ ] 暂停/恢复按钮正常工作
- [ ] 休息窗口正常弹出（黑屏或图片轮播）
- [ ] 休息结束自动恢复倒计时
- [ ] 修改密码功能正常
- [ ] 忘记密码重置功能正常
- [ ] 系统托盘图标显示正常
- [ ] 开机自启动设置有效
- [ ] 配置文件正确保存和加载

### 4.2 自动化测试验证
```bash
cd eyesharp.Tests
dotnet test --verbosity minimal
```
期望结果：`通过: 46，失败: 0`

## 五、回滚方案

### 5.1 配置文件损坏回滚
1. 程序自动检测配置损坏
2. 自动备份损坏配置到 `config.corrupted.YYYYMMDD_HHmmss.json`
3. 恢复默认配置并弹出提示对话框
4. 用户确认后继续使用

### 5.2 版本回滚
如需回滚到上一版本：
1. 停止运行中的程序
2. 替换 `eyesharp.exe` 为旧版本
3. 重新启动程序

### 5.3 完整重置
如需完全重置所有配置：
1. 退出程序
2. 删除 `config.json` 和 `config.backup.json`
3. 重新启动程序，按首次启动流程设置

## 六、常见问题

### Q1: 程序无法启动
- 检查是否满足系统要求
- 检查是否已安装 .NET 8.0 运行时（框架依赖版本）
- 查看 `logs/` 目录中的错误日志

### Q2: 开机自启动不生效
- 检查程序是否有写入注册表权限
- 检查 Windows 启动应用设置中是否被禁用

### Q3: 休息窗口无法显示
- 检查是否设置了自定义图片路径（图片模式）
- 检查图片路径是否存在有效图片文件

### Q4: 忘记密码
- 使用主界面的"忘记密码"功能
- 这将清空所有配置，下次启动需重新设置

## 七、联系方式

- 项目地址：[GitHub Repository]
- 问题反馈：[Issues]
- 版本：v1.0.0

---

**最后更新**：2026-03-01
