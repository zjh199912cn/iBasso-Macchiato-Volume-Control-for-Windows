namespace MacchiatoTray;

public class VolumeOSD : Form
{
    readonly System.Windows.Forms.Timer _closeTimer = new() { Interval = 1200 };
    readonly Label _deviceLabel, _iconLabel, _textLabel;
    readonly TableLayoutPanel _panel;

    Font? _cachedDeviceFont, _cachedIconFont, _cachedTextFont;

    public VolumeOSD()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        AutoScaleMode = AutoScaleMode.None; // 手动缩放，避免 DPI 双重缩放
        Opacity = 0.85;
        BackColor = Color.FromArgb(32, 32, 32);
        MinimumSize = Size.Empty;

        _panel = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(14, 8, 14, 8),
            BackColor = Color.FromArgb(32, 32, 32),
        };

        _deviceLabel = new Label
        {
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 8),
            Anchor = AnchorStyles.None,
            Margin = new Padding(0),
            BackColor = Color.FromArgb(32, 32, 32),
            Visible = false,
        };

        _iconLabel = new Label
        {
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 18),
            Anchor = AnchorStyles.None,
            Margin = new Padding(0),
            BackColor = Color.FromArgb(32, 32, 32),
            ForeColor = Color.White,
        };

        _textLabel = new Label
        {
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            Anchor = AnchorStyles.None,
            Margin = new Padding(0),
            BackColor = Color.FromArgb(32, 32, 32),
            ForeColor = Color.White,
        };

        _panel.Controls.Add(_deviceLabel, 0, 0);
        _panel.Controls.Add(_iconLabel, 0, 1);
        _panel.Controls.Add(_textLabel, 0, 2);
        Controls.Add(_panel);

        _closeTimer.Tick += (_, _) =>
        {
            _closeTimer.Stop();
            Hide();
        };
    }

    public void RefreshOSD(bool muted, int volume, string? deviceName)
    {
        var s = AppSettings.Load();
        float sf = s.ScalePercent / 100f;

        Color bg = Color.FromArgb(s.BgR, s.BgG, s.BgB);
        BackColor = bg;
        _panel.BackColor = bg;
        _deviceLabel.BackColor = bg;
        _iconLabel.BackColor = bg;
        _textLabel.BackColor = bg;

        if (s.ShowDeviceName && !string.IsNullOrEmpty(deviceName))
        {
            _deviceLabel.Text = deviceName;
            _deviceLabel.ForeColor = Color.FromArgb(180, 180, 180);
            SetLabelFont(ref _cachedDeviceFont, _deviceLabel, s.FontName, 8 * sf, FontStyle.Regular);
            _deviceLabel.Visible = true;
        }
        else
        {
            _deviceLabel.Visible = false;
        }

        _iconLabel.Text = muted ? "🔇" : "🔊";
        SetLabelFont(ref _cachedIconFont, _iconLabel, s.FontName, 18 * sf, FontStyle.Regular);

        _textLabel.Text = muted ? "静音" : $"{volume}%";
        _textLabel.ForeColor = muted ? Color.FromArgb(255, 130, 130) : Color.White;
        var style = s.Bold ? FontStyle.Bold : FontStyle.Regular;
        SetLabelFont(ref _cachedTextFont, _textLabel, s.FontName, 13 * sf, style);

        var screen = Screen.FromPoint(Cursor.Position);
        Left = screen.WorkingArea.Left + (screen.WorkingArea.Width - Width) / 2;
        Top = screen.WorkingArea.Top + (screen.WorkingArea.Height - Height) / 2;

        _closeTimer.Stop();
        _closeTimer.Start();
        if (!Visible) Show();
    }

    /// <summary>先赋值新字体再释放旧字体，避免 Paint 事件使用已释放的 GDI 对象</summary>
    static void SetLabelFont(ref Font? cache, Label label, string familyName,
        float emSize, FontStyle style)
    {
        var old = cache;
        cache = new Font(familyName, emSize, style);
        label.Font = cache;
        old?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _closeTimer?.Dispose();
            _cachedDeviceFont?.Dispose();
            _cachedIconFont?.Dispose();
            _cachedTextFont?.Dispose();
        }
        base.Dispose(disposing);
    }
}
