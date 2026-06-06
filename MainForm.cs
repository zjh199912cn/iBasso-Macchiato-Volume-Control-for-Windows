using Microsoft.Win32;
using System.Diagnostics;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace MacchiatoTray;

public class MainForm : Form
{
    readonly MacchiatoDevice _device = new();
    readonly NotifyIcon _trayIcon = new();
    nint _hookHandle;
    VolumeOSD? _osd;
    ContextMenuStrip? _contextMenu;
    nint _deviceNotifyHandle;

    const int WM_APP_WHEEL = 0x8001;

    int _pendingDelta;
    int _targetVolume = -1;
    readonly System.Windows.Forms.Timer _debounceTimer = new() { Interval = 16 };
    readonly System.Windows.Forms.Timer _osdShowTimer = new() { Interval = 500 };

    readonly Win32.LowLevelMouseProc _hookProc;

    const string RegPath = @"Software\MacchiatoTray";
    bool _showVolumeOSD = true;
    bool _gameMode = true;
    bool _hookSuspended;
    nint _winEventHook;
    GCHandle _winEventHookGcHandle;

    Icon? _cachedNormalIcon, _cachedMuteIcon;
    bool _iconsFromDll = true;
    bool _registryWarningShown;

    DateTime _lastIconMove = DateTime.MinValue;

    delegate void WinEventProc(nint hWinEventHook, uint eventType, nint hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x80;
            return cp;
        }
    }

    public MainForm()
    {
        this.Location = new Point(-32000, -32000);
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.None;
        Load += MainForm_Load;
        FormClosing += MainForm_FormClosing;
        _hookProc = HookCallback;
        _debounceTimer.Tick += (_, _) =>
        {
            _debounceTimer.Stop();
            if (_targetVolume >= 0 && _targetVolume != _device.Volume)
            {
                _device.CommitVolume(_targetVolume);
                UpdateTrayIcon();
            }
            _targetVolume = -1;
        };
        _osdShowTimer.Tick += OsdShowTimer_Tick;
        LoadOsdSetting();
    }

    void LoadOsdSetting()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegPath);
            if (key != null)
            {
                var v = key.GetValue("ShowVolumeOSD");
                if (v is int i) _showVolumeOSD = i != 0;
                v = key.GetValue("GameMode");
                if (v is int j) _gameMode = j != 0;
            }
        }
        catch (Exception ex) { Debug.WriteLine($"LoadOsdSetting: {ex.Message}"); }
    }

    void SaveOsdSetting()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegPath);
            key?.SetValue("ShowVolumeOSD", _showVolumeOSD ? 1 : 0);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SaveOsdSetting: {ex.Message}");
            ShowRegistryWarning();
        }
    }

    void SaveGameModeSetting()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegPath);
            key?.SetValue("GameMode", _gameMode ? 1 : 0);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SaveGameModeSetting: {ex.Message}");
            ShowRegistryWarning();
        }
    }

    void ShowRegistryWarning()
    {
        if (_registryWarningShown) return;
        _registryWarningShown = true;
        _trayIcon.ShowBalloonTip(3000, "Macchiato Tray",
            "无法保存设置，请检查注册表权限。", ToolTipIcon.Warning);
    }

    string GetSysPath(string dll) => Path.Combine(Environment.SystemDirectory, dll);

    Icon? LoadIconFromDll(string dllPath, int index)
    {
        if (!File.Exists(dllPath)) return null;
        nint h = ExtractIcon(IntPtr.Zero, dllPath, index);
        if (h == 0) return null;
        var icon = (Icon)Icon.FromHandle(h).Clone();
        DestroyIcon(h);
        return icon;
    }

    Icon GetTrayIcon(bool muted)
    {
        string path = GetSysPath("SndVolSSO.dll");
        int idx = muted ? 1 : 0;
        var icon = LoadIconFromDll(path, idx);
        if (icon != null) return icon;

        path = GetSysPath("mmres.dll");
        idx = muted ? 3 : 1;
        icon = LoadIconFromDll(path, idx);
        if (icon != null) return icon;

        _iconsFromDll = false;
        return SystemIcons.Information;
    }

    Icon MakeTrayIcon()
    {
        bool muted = _device.Muted || _device.Volume == 0;
        if (muted)
        {
            if (_cachedMuteIcon == null && _iconsFromDll)
                _cachedMuteIcon = GetTrayIcon(true);
            return _cachedMuteIcon ?? GetTrayIcon(true);
        }
        else
        {
            if (_cachedNormalIcon == null && _iconsFromDll)
                _cachedNormalIcon = GetTrayIcon(false);
            return _cachedNormalIcon ?? GetTrayIcon(false);
        }
    }

    void InvalidateIconCache()
    {
        _cachedMuteIcon?.Dispose();
        _cachedNormalIcon?.Dispose();
        _cachedMuteIcon = null;
        _cachedNormalIcon = null;
        _iconsFromDll = true;
    }

    void UpdateTrayIcon()
    {
        _trayIcon.Icon = MakeTrayIcon();
        _trayIcon.Text = MakeTip();
    }

    async void MainForm_Load(object? sender, EventArgs e)
    {
        this.Location = new Point(-32000, -32000);
        this.Hide();

        _osd = new VolumeOSD();
        if (_device.FindAndOpen())
        {
            Console.WriteLine("设备已连接，正在异步读取音量…");
            try { await _device.ReadVolumeAsync(); }
            catch (Exception ex) { Debug.WriteLine($"初始读取失败: {ex.Message}"); }
        }
        else
            Console.WriteLine("⚠ 未检测到设备");

        _trayIcon.Icon = MakeTrayIcon();
        _trayIcon.Text = MakeTip();
        _trayIcon.Visible = true;
        _trayIcon.MouseDown += TrayIcon_MouseDown;
        _trayIcon.MouseMove += (_, _) => _lastIconMove = DateTime.Now;

        _contextMenu = new ContextMenuStrip();
        _contextMenu.Opening += ContextMenu_Opening;
        _trayIcon.ContextMenuStrip = _contextMenu;

        InstallHook();
        InstallWinEventHook();
        _deviceNotifyHandle = HidMonitor.Register(Handle);

        Console.WriteLine("🚀 已启动");
    }

    void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_deviceNotifyHandle != 0)
            HidMonitor.UnregisterDeviceNotification(_deviceNotifyHandle);
        UninstallWinEventHook();
        _osd?.Close(); _osd = null;
        UninstallHook();
        _trayIcon.Visible = false;
        _trayIcon.Icon?.Dispose();
        _contextMenu?.Dispose();
        _device.Dispose();
        InvalidateIconCache();
    }

    void TrayIcon_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (_device.Connected)
            {
                _device.ToggleMute();
                UpdateTrayIcon();
                ShowOSD();
            }
        }
    }

    void ContextMenu_Opening(object? sender, CancelEventArgs e)
    {
        if (_contextMenu == null) return;
        _contextMenu.Items.Clear();

        _contextMenu.Items.Add(_device.Connected
            ? $"音量: {(_device.VolumeUnknown ? "—" : $"{_device.Volume}%")}"
            : "未连接").Enabled = false;
        _contextMenu.Items.Add(new ToolStripSeparator());

        _contextMenu.Items.Add("Web 控制台", null, (_, _) =>
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://ibasso.cn/uac/#/device/Macchiato",
                UseShellExecute = true
            });
        });
        _contextMenu.Items.Add(new ToolStripSeparator());

        var osdItem = new ToolStripMenuItem("音量弹窗") { Checked = _showVolumeOSD };

        osdItem.Click += (_, _) => { _showVolumeOSD = !_showVolumeOSD; SaveOsdSetting(); };
        _contextMenu.Items.Add(osdItem);

        var gmItem = new ToolStripMenuItem("全屏时暂停") { Checked = _gameMode };
        gmItem.Click += (_, _) =>
        {
            _gameMode = !_gameMode;
            SaveGameModeSetting();
            if (!_gameMode && _hookSuspended) { _hookSuspended = false; Console.WriteLine("恢复钩子"); }
        };
        _contextMenu.Items.Add(gmItem);

        _contextMenu.Items.Add("OSD 设置", null, (_, _) =>
        {
            using var s = new SettingForm();
            s.ShowInTaskbar = false;
            s.ShowDialog();
            _osd?.ApplySettings(AppSettings.Load());
        });
        _contextMenu.Items.Add(new ToolStripSeparator());

        var auItem = new ToolStripMenuItem("开机自启") { Checked = IsAutostartEnabled() };
        auItem.Click += (_, _) => ToggleAutostart();
        _contextMenu.Items.Add(auItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add("退出", null, (_, _) => BeginInvoke(() => Close()));
    }

    static bool IsAutostartEnabled()
    {
        using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        return k?.GetValue("MacchiatoTray") != null;
    }

    static void ToggleAutostart()
    {
        using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (k == null) return;
        if (k.GetValue("MacchiatoTray") != null)
            k.DeleteValue("MacchiatoTray");
        else
            k.SetValue("MacchiatoTray", $"\"{Application.ExecutablePath}\"");
    }

    void InstallWinEventHook()
    {
        var p = new WinEventProc(OnForegroundChanged);
        _winEventHookGcHandle = GCHandle.Alloc(p);
        _winEventHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, p, 0, 0, WINEVENT_OUTOFCONTEXT);
        CheckFullscreen();
    }

    void UninstallWinEventHook()
    {
        if (_winEventHook != IntPtr.Zero) { UnhookWinEvent(_winEventHook); _winEventHook = IntPtr.Zero; }
        if (_winEventHookGcHandle.IsAllocated) _winEventHookGcHandle.Free();
    }

    void OnForegroundChanged(nint h, uint t, nint hw, int o, int c, uint th, uint ms)
        => CheckFullscreen();

    void CheckFullscreen()
    {
        if (!_gameMode) return;
        bool f = IsForegroundFullscreen();
        if (f && !_hookSuspended) { _hookSuspended = true; Console.WriteLine("🎮 全屏，暂停钩子"); }
        else if (!f && _hookSuspended) { _hookSuspended = false; Console.WriteLine("🖥 桌面，恢复钩子"); }
    }

    bool IsForegroundFullscreen()
    {
        var fg = GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == Handle) return false;
        int s = GetWindowLong(fg, GWL_STYLE);
        if ((s & WS_BORDER) != 0 || (s & WS_CAPTION) != 0) return false;
        Win32.GetWindowRect(fg, out Win32.RECT wr);
        nint mon = MonitorFromWindow(fg, MONITOR_DEFAULTTONEAREST);
        var mi = new MONITORINFO();
        mi.cbSize = Marshal.SizeOf(mi);
        if (!GetMonitorInfo(mon, ref mi)) return false;
        return (wr.right - wr.left) >= (mi.rcMonitor.right - mi.rcMonitor.left)
            && (wr.bottom - wr.top) >= (mi.rcMonitor.bottom - mi.rcMonitor.top);
    }

    // ── Win32 API ──
    [DllImport("user32.dll")]
    static extern nint SetWinEventHook(uint a, uint b, nint c, WinEventProc d, int e, int f, uint g);
    [DllImport("user32.dll")]
    static extern bool UnhookWinEvent(nint h);
    [DllImport("user32.dll")]
    static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]
    static extern int GetWindowLong(nint h, int i);
    [DllImport("user32.dll")]
    static extern nint MonitorFromWindow(nint h, uint f);
    [DllImport("user32.dll")]
    static extern bool GetMonitorInfo(nint m, ref MONITORINFO mi);
    [DllImport("user32.dll")]
    static extern bool DestroyIcon(nint hIcon);
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    static extern nint ExtractIcon(nint hInst, string file, int index);

    // ── 托盘区域检测 ──
    [DllImport("user32.dll")]
    static extern IntPtr WindowFromPoint(int x, int y);

    [DllImport("user32.dll")]
    static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    static extern IntPtr GetParent(IntPtr hWnd);

    static bool IsCursorOnTaskbar()
    {
        Win32.GetCursorPos(out Win32.POINT pt);
        IntPtr hWnd = WindowFromPoint(pt.x, pt.y);
        if (hWnd == IntPtr.Zero) return false;

        var sb = new StringBuilder(256);
        for (int i = 0; i < 5; i++)
        {
            GetClassName(hWnd, sb, 256);
            string cls = sb.ToString();
            if (cls is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")
                return true;
            hWnd = GetParent(hWnd);
        }
        return false;
    }

    const int GWL_STYLE = -16, WS_BORDER = 0x00800000, WS_CAPTION = 0x00C00000;
    const uint MONITOR_DEFAULTTONEAREST = 2, WINEVENT_OUTOFCONTEXT = 0, EVENT_SYSTEM_FOREGROUND = 3;

    [StructLayout(LayoutKind.Sequential)]
    struct MONITORINFO
    {
        public int cbSize;
        public Win32.RECT rcMonitor;
        public Win32.RECT rcWork;
        public uint dwFlags;
    }

    void ShowOSD()
    {
        _osd?.RefreshOSD(_device.Muted, _device.Volume,
            _device.Connected ? "Macchiato" : null);
    }

    void ShowOSD(int pendingVolume, bool muted)
    {
        _osd?.RefreshOSD(muted, pendingVolume,
            _device.Connected ? "Macchiato" : null);
    }

    string MakeTip()
    {
        if (!_device.Connected) return "iBasso Macchiato - 未连接";
        if (_device.VolumeUnknown) return "iBasso Macchiato - 读取中…";
        if (_device.Muted) return "iBasso Macchiato - 已静音";
        return $"iBasso Macchiato - {_device.Volume}%";
    }

    void InstallHook()
    {
        _hookHandle = Win32.SetWindowsHookEx(Win32.WH_MOUSE_LL, _hookProc, IntPtr.Zero, 0);
        Console.WriteLine(_hookHandle == 0
            ? $"❌ 钩子失败 ({Marshal.GetLastWin32Error()})"
            : "✓ 钩子已安装");
    }

    void UninstallHook()
    {
        if (_hookHandle != 0) { Win32.UnhookWindowsHookEx(_hookHandle); _hookHandle = 0; }
    }

    nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        try
        {
            if (_hookSuspended)
                return Win32.CallNextHookEx(_hookHandle, nCode, wParam, lParam);

            if (nCode >= 0)
            {
                int msg = (int)wParam;

                // 任意鼠标点击：非任务栏区域则打断滚轮并关闭 OSD
                if (msg is Win32.WM_LBUTTONDOWN or Win32.WM_RBUTTONDOWN or Win32.WM_MBUTTONDOWN)
                {
                    if (!IsCursorOnTaskbar())
                    {
                        _lastIconMove = DateTime.MinValue;
                        _osd?.Hide();
                    }
                }

                // 滚轮：时间戳窗口内有效，滚轮事件本身也会续期窗口
                if (msg == Win32.WM_MOUSEWHEEL
                    && (DateTime.Now - _lastIconMove).TotalMilliseconds < 2000)
                {
                    _lastIconMove = DateTime.Now;
                    var hs = Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);
                    Win32.PostMessage(Handle, WM_APP_WHEEL, (nint)(short)(hs.mouseData >> 16), 0);
                }
            }
        }
        catch (Exception ex) { Debug.WriteLine($"HookCallback error: {ex}"); }
        return Win32.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_APP_WHEEL) { HandleWheel((short)(int)m.WParam); return; }
        if (m.Msg == HidMonitor.WM_DEVICECHANGE) { HandleDeviceChange((int)m.WParam); return; }
        base.WndProc(ref m);
    }

    void HandleDeviceChange(int wParam)
    {
        if (wParam == HidMonitor.DBT_DEVICEREMOVECOMPLETE)
        {
            if (_device.Connected) { Console.WriteLine("🔌 设备已拔出"); _device.Close(); UpdateTrayIcon(); }
        }
        else if (wParam == HidMonitor.DBT_DEVICEARRIVAL)
        {
            if (!_device.Connected && HidMonitor.IsDevicePresent(0x0661, MacchiatoDevice.ProductIds))
            {
                Console.WriteLine("🔌 设备已插入");
                InvalidateIconCache();
                _device.FindAndOpen();
                UpdateTrayIcon();
            }
        }
    }

    void HandleWheel(short delta)
    {
        if (!_device.Connected) return;

        int step = delta > 0 ? 2 : -2;

        _pendingDelta += step;

        // 滚动中只显示方向符号
        if (_showVolumeOSD && _pendingDelta != 0)
            _osd?.ShowSymbol(_pendingDelta > 0 ? "▲" : "▼",
                _device.Muted, _device.Connected ? "Macchiato" : null);

        // 实时调整 DAC 音量（16ms 防抖批量写入）
        int baseVol = _targetVolume >= 0 ? _targetVolume : _device.Volume;
        if (baseVol < 0) baseVol = 50;
        _targetVolume = Math.Clamp(baseVol + step, 0, 100);
        _debounceTimer.Stop();
        _debounceTimer.Start();

        // 停止滚动 500ms 后显示实际数值
        _osdShowTimer.Stop();
        _osdShowTimer.Start();
    }

    void OsdShowTimer_Tick(object? sender, EventArgs e)
    {
        _osdShowTimer.Stop();

        int totalDelta = _pendingDelta;
        if (totalDelta == 0) return;
        _pendingDelta = 0;

        // 防抖已提交到 DAC，_device.Volume 即实际值，无需再读设备
        if (_device.VolumeUnknown) { UpdateTrayIcon(); return; }
        if (_showVolumeOSD) ShowOSD();
    }
}
