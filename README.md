# Macchiato Tray

适用于 **iBasso Macchiato USB DAC** 的 Windows 系统托盘音量控制程序。

对标 Twinkle Tray 的交互体验：鼠标在托盘图标上滚轮调音量，左键切静音，右键弹出菜单，带半透明 OSD 弹窗。全程由 **DeepSeek AI** 编写。

## 功能

| 操作 | 说明 |
|------|------|
| 🖱️ 托盘图标滚轮 | 调节音量（三档：慢 ±1 / 中 ±2 / 快 ±4） |
| 🖱️ 左键托盘图标 | 切换静音 |
| 🖱️ 右键托盘图标 | 弹窗开关 · 全屏暂停钩子 · OSD 设置 · 开机自启 · 退出 |
| 📺 OSD 弹窗 | 屏幕居中，半透明，1.2 秒消失，可自定义样式 |
| 🎮 全屏暂停钩子 | 进入全屏时自动挂起鼠标钩子，防反作弊误判 |
| 🔌 热插拔 | 设备插拔自动连接/断开 |
| 🎨 OSD 自定义 | 缩放、背景色、字体、字号、加粗、设备名 |

## 系统要求

- Windows 10 / 11 x64
- .NET 8 Runtime（自包含单 exe 版本无需安装）
- iBasso Macchiato USB DAC（VID `0x0661` PID `0x0881` / `0x0882`）

## 下载

从 [Releases](../../releases) 下载 `MacchiatoTray.exe`，单文件双击运行，无需安装。

## 构建

```powershell
git clone <repo-url>
cd macchiato-tray
dotnet restore
dotnet build -c Release

# 单文件发布（自包含，无需 .NET Runtime）
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true -p:InvariantGlobalization=true -p:EnableCompressionInSingleFile=true
