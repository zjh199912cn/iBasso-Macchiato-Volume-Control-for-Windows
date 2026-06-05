
# Macchiato Tray

适用于 **iBasso Macchiato USB DAC** 的 Windows 系统托盘音量控制程序。
基于https://ibasso.cn/uac/#/device/Macchiato WEB控制台，抓取设备控制js文件，获取音量调节代码，然后再让deepseek编写调试与代码审核。
对标 Twinkle Tray 的交互体验：托盘图标滚轮调音量、左键切静音、右键菜单管理、半透明 OSD 弹窗。  
**本项目由 DeepSeek AI 编写。**

<img width="227" height="250" alt="image" src="https://github.com/user-attachments/assets/8ba2b7a7-04ad-4528-9c2d-3bbe1e60ea0d" />
<img width="160" height="161" alt="image" src="https://github.com/user-attachments/assets/bd53f0c7-b05c-41e5-ac36-de404600b797" />
<img width="151" height="160" alt="image" src="https://github.com/user-attachments/assets/5910c997-f834-408b-877b-4b7eb09a5bd8" />


## 功能

- 🖱️ 托盘图标滚轮调节音量（±2）
- 🖱️ 左键托盘图标切换静音
- 🖱️ 右键菜单：弹窗开关、全屏暂停钩子、OSD 设置、开机自启、退出
- 📺 屏幕中央半透明 OSD 弹窗，1.2 秒消失，可自定义
- 🎮 全屏时自动挂起鼠标钩子，防反作弊误判
- 🔌 设备热插拔自动连接/断开
- 🎨 OSD 自定义：缩放、背景色、字体、字号、加粗、设备名

## 系统要求

- Windows 10 / 11 x64
- iBasso Macchiato USB DAC（VID `0x0661`，PID `0x0881` 或 `0x0882`）
- 自包含版本无需安装 .NET Runtime

## 使用

1. 从 [Releases](../../releases) 下载 `MacchiatoTray.exe`
2. 双击运行，托盘出现扬声器图标
3. 滚轮调音量 / 左键静音 / 右键设置

## 构建

```powershell
git clone <repo-url>
cd macchiato-tray
dotnet restore
dotnet build -c Release

# 发布单文件 EXE
dotnet publish -c Release -r win-x64 ^
  -p:PublishSingleFile=true ^
  -p:SelfContained=true ^
  -p:InvariantGlobalization=true ^
  -p:EnableCompressionInSingleFile=true

```

输出：`bin\Release\net8.0-windows\win-x64\publish\MacchiatoTray.exe`

## 技术栈

C# 12 · .NET 8 · WinForms · [HidSharp](https://github.com/IntergatedCircuits/HidSharp) 2.6.2 · WH_MOUSE_LL 低级鼠标钩子 · Feature Report（reportId `0x4B`）· WinEventHook 全屏检测 · 图标从 SndVolSSO.dll / mmres.dll 提取 · 注册表配置

## 文件

| 文件 | 职责 |
|------|------|
| `Program.cs` | 入口，单实例互斥体 |
| `MainForm.cs` | 主窗口（隐藏）、托盘图标、右键菜单、钩子、全屏检测 |
| `MacchiatoDevice.cs` | HID 通信：连接、音量读写、静音、重连 |
| `HidMonitor.cs` | SetupAPI 枚举 + RegisterDeviceNotification 插拔事件 |
| `VolumeOSD.cs` | 半透明 OSD 弹窗，字体缓存 |
| `SettingForm.cs` | 深色主题 OSD 设置窗口 |
| `AppSettings.cs` | 注册表配置持久化 |
| `Win32.cs` | P/Invoke 声明 |

## 常见问题

**Q：滚轮没反应？**  
确认鼠标悬停在托盘图标上。离开图标超过 0.5 秒或点击其他地方后滚轮自动失效，重新移回图标即可恢复。


**Q：OSD 不弹？**  
右键托盘图标，勾选「音量弹窗」。

**Q：提示"已在运行"？**  
程序已启动，检查系统托盘。可从任务管理器强制关闭。

**Q：设备未连接？**  
确认 Macchiato 已插入 USB，VID/PID 匹配。

**Q：静音后取消音量跳到奇怪的值？**  
音量调到 0 后静音再取消会恢复为 10%，这是防止误操作导致音量过大的保护设计。

---

## 致谢

本项目由 **DeepSeek AI** 编写完成。
<img width="1482" height="1214" alt="image" src="https://github.com/user-attachments/assets/bf7b5ba4-f6f0-4570-a29d-c94a0ece57e0" />

从 Python 到 C# 的迁移、Win32 钩子方案、HID 协议适配、GDI 字体生命周期管理、图标缓存策略、全屏检测实现、热插拔事件驱动重构、单文件发布配置，以及全过程的调试迭代——所有代码均由 DeepSeek 协助生成。

<img width="1448" height="1086" alt="7c583e3a020d422d4172286db1425f75" src="https://github.com/user-attachments/assets/62c42dc7-ff4a-471a-a1da-61c6759c7bcd" />

## License

无任何限制。允许任意使用、修改、分发，无需署名，无需保留许可声明。
```
