using Microsoft.Win32;

namespace MacchiatoTray;

public class SettingForm : Form
{
    ComboBox _bgBox, _fontBox;
    NumericUpDown _sizeBox, _scaleBox;
    CheckBox _boldBox, _showDeviceBox;

    public SettingForm()
    {
        Icon = SystemIcons.Information;
        ShowInTaskbar = false;

        Text = "OSD 设置";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Size = new Size(320, 310);
        BackColor = Color.FromArgb(40, 40, 40);
        ForeColor = Color.White;

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(10),
            BackColor = Color.FromArgb(40, 40, 40),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));

        panel.Controls.Add(MakeLabel("OSD 缩放"), 0, 0);
        _scaleBox = new NumericUpDown
        {
            Minimum = 50, Maximum = 250, Increment = 10, Value = 100,
            BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White,
        };
        panel.Controls.Add(_scaleBox, 1, 0);

        panel.Controls.Add(MakeLabel("背景色"), 0, 1);
        _bgBox = MakeCombo(new[] { "深灰", "纯黑", "深蓝", "深红" });
        panel.Controls.Add(_bgBox, 1, 1);

        panel.Controls.Add(MakeLabel("字体"), 0, 2);
        _fontBox = MakeCombo(new[] { "Segoe UI", "Microsoft YaHei", "Consolas", "Arial" });
        panel.Controls.Add(_fontBox, 1, 2);

        panel.Controls.Add(MakeLabel("字号"), 0, 3);
        _sizeBox = new NumericUpDown { Minimum = 9, Maximum = 24, BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White };
        panel.Controls.Add(_sizeBox, 1, 3);

        panel.Controls.Add(MakeLabel("加粗"), 0, 4);
        _boldBox = new CheckBox { ForeColor = Color.White };
        panel.Controls.Add(_boldBox, 1, 4);

        panel.Controls.Add(MakeLabel("显示设备名"), 0, 5);
        _showDeviceBox = new CheckBox { ForeColor = Color.White };
        panel.Controls.Add(_showDeviceBox, 1, 5);

        var btnPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        var cancelBtn = new Button { Text = "取消", BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White };
        var saveBtn = new Button { Text = "保存", BackColor = Color.FromArgb(60, 120, 200), ForeColor = Color.White };
        cancelBtn.Click += (_, _) => Close();
        saveBtn.Click += (_, _) => { Save(); Close(); };
        btnPanel.Controls.Add(saveBtn);
        btnPanel.Controls.Add(cancelBtn);
        panel.Controls.Add(btnPanel, 1, 6);

        Controls.Add(panel);
        LoadSettings();
    }

    Label MakeLabel(string text)
        => new() { Text = text, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.White };

    ComboBox MakeCombo(string[] items)
    {
        var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White };
        c.Items.AddRange(items);
        return c;
    }

    void LoadSettings()
    {
        var k = AppSettings.Load();
        _scaleBox.Value = k.ScalePercent;
        _bgBox.SelectedIndex = k.BgIndex;
        _fontBox.Text = k.FontName;
        _sizeBox.Value = k.FontSize;
        _boldBox.Checked = k.Bold;
        _showDeviceBox.Checked = k.ShowDeviceName;
    }

    void Save()
    {
        AppSettings.Save(new AppSettings
        {
            ScalePercent = (int)_scaleBox.Value,
            BgIndex = _bgBox.SelectedIndex,
            FontName = _fontBox.Text,
            FontSize = (int)_sizeBox.Value,
            Bold = _boldBox.Checked,
            ShowDeviceName = _showDeviceBox.Checked,
        });
    }
}
