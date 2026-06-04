using Microsoft.Win32;
using System.Diagnostics;

namespace MacchiatoTray;

public class AppSettings
{
    public int BgIndex;
    public string FontName = "Segoe UI";
    public int FontSize = 13;
    public bool Bold;
    public bool ShowDeviceName;
    public int ScalePercent = 100;

    public int BgR => BgIndex switch
    {
        1 => 0, 2 => 22, 3 => 60, _ => 32
    };
    public int BgG => BgIndex switch
    {
        1 => 0, 2 => 22, 3 => 10, _ => 32
    };
    public int BgB => BgIndex switch
    {
        1 => 0, 2 => 64, 3 => 10, _ => 32
    };

    public float ScaleFactor => ScalePercent / 100f;

    public static AppSettings Load()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\MacchiatoTray");
            if (key != null)
            {
                return new AppSettings
                {
                    BgIndex = (int)(key.GetValue("BgIndex") ?? 0),
                    FontName = (string)(key.GetValue("FontName") ?? "Segoe UI"),
                    FontSize = (int)(key.GetValue("FontSize") ?? 13),
                    Bold = (int)(key.GetValue("Bold") ?? 0) != 0,
                    ShowDeviceName = (int)(key.GetValue("ShowDeviceName") ?? 0) != 0,
                    ScalePercent = (int)(key.GetValue("ScalePercent") ?? 100),
                };
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AppSettings.Load: {ex.Message}");
        }
        return new AppSettings();
    }

    public static void Save(AppSettings s)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\MacchiatoTray");
            key?.SetValue("BgIndex", s.BgIndex);
            key?.SetValue("FontName", s.FontName);
            key?.SetValue("FontSize", s.FontSize);
            key?.SetValue("Bold", s.Bold ? 1 : 0);
            key?.SetValue("ShowDeviceName", s.ShowDeviceName ? 1 : 0);
            key?.SetValue("ScalePercent", s.ScalePercent);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AppSettings.Save: {ex.Message}");
        }
    }
}
