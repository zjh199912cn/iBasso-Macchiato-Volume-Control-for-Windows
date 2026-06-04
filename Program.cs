namespace MacchiatoTray;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "Global\\MacchiatoTray_SI", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("Macchiato Tray 已在运行", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());

        GC.KeepAlive(mutex);
    }
}
