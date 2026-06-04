using HidSharp;
using System.Diagnostics;

namespace MacchiatoTray;

public class MacchiatoDevice : IDisposable
{
    const int VendorId = 0x0661;
    public static readonly int[] ProductIds = [0x0881, 0x0882];
    const byte ReportId = 0x4B;

    HidDevice? _device;
    HidStream? _stream;
    int _volume = -1;
    bool _muted;
    int _preMuteVolume = -1;

    const int MaxReconnectAttempts = 1;
    int _reconnectCount = 0;

    public int Volume => _volume;
    public bool VolumeUnknown => _volume < 0;
    public bool Muted => _muted;
    public bool Connected => _stream != null;

    // ─────────────────────────────────────────
    //  连接 / 断开
    // ─────────────────────────────────────────
    public bool FindAndOpen()
    {
        foreach (var device in DeviceList.Local.GetHidDevices())
        {
            if (device.VendorID == VendorId && ProductIds.Contains(device.ProductID))
                return Open(device);
        }
        return false;
    }

    bool Open(HidDevice device)
    {
        try
        {
            _device = device;
            _stream = device.Open();
            _stream.ReadTimeout = 300;
            _reconnectCount = 0;
            // 异步读取初始音量
            _ = ReadVolumeAsync();
            Debug.WriteLine($"✓ 已连接: {device.GetProductName()}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"✗ 连接失败: {ex.Message}");
            return false;
        }
    }

    public void Close()
    {
        _stream?.Close();
        _stream = null;
        _device = null;
    }

    bool TryReconnect()
    {
        if (_reconnectCount >= MaxReconnectAttempts)
            return false;

        _reconnectCount++;
        Debug.WriteLine($"🔄 尝试重连 (第 {_reconnectCount} 次)");
        Close();
        if (FindAndOpen())
        {
            _reconnectCount = 0;
            return true;
        }
        return false;
    }

    // ─────────────────────────────────────────
    //  音量读取（异步，适配 HidSharp 2.x）
    // ─────────────────────────────────────────
    public async Task ReadVolumeAsync()
    {
        if (_stream == null) return;
        try
        {
            var cmd = new byte[64];
            cmd[0] = ReportId; cmd[1] = 0x80; cmd[2] = 0x3F;
            cmd[3] = 0x08; cmd[4] = 0x42; cmd[5] = 0x10;
            await _stream.WriteAsync(cmd, 0, cmd.Length);

            var buffer = new byte[64];
            for (int attempt = 0; attempt < 5; attempt++)
            {
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead < 6) continue;

                for (int i = 0; i <= bytesRead - 6; i++)
                {
                    if (buffer[i] == 0x10 && i + 1 < bytesRead)
                    {
                        _volume = Math.Clamp((int)buffer[i + 1], 0, 100);
                        Debug.WriteLine($"  当前音量: {_volume}% (异步读取成功)");
                        return;
                    }
                }
            }
            Debug.WriteLine("  读取音量超时（未找到有效响应）");
        }
        catch (TimeoutException)
        {
            Debug.WriteLine("  读取音量超时");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"  读取音量失败: {ex.Message}");
        }
    }

    // 同步版本（兼容旧调用，但不推荐）
    public void ReadVolume()
    {
        ReadVolumeAsync().Wait();
    }

    // ─────────────────────────────────────────
    //  音量写入
    // ─────────────────────────────────────────
    bool SetVolumeInternal(int vol)
    {
        if (_stream == null) return false;
        vol = Math.Clamp(vol, 0, 100);
        try
        {
            var buf = new byte[64];
            buf[0] = ReportId; buf[1] = 0x01; buf[2] = 0x3F;
            buf[3] = 0x08; buf[4] = 0x01; buf[5] = 0x10;
            buf[6] = (byte)vol; buf[7] = (byte)vol;
            _stream.Write(buf);
            _volume = vol;
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SetVolumeInternal 失败 (vol={vol}): {ex.Message}");
            Close();
            return false;
        }
    }

    public bool CommitVolume(int volume)
    {
        if (!Connected)
        {
            if (!TryReconnect())
                return false;
        }
        if (SetVolumeInternal(volume))
            return true;
        if (TryReconnect())
            return SetVolumeInternal(volume);
        return false;
    }

    public int PreviewAdjust(int delta)
    {
        int current = _muted ? (_preMuteVolume > 0 ? _preMuteVolume : 50) : _volume;
        if (current < 0) current = 50;
        return Math.Clamp(current + delta, 0, 100);
    }

    public void AdjustVolume(int delta)
    {
        int newVol;
        if (_muted)
        {
            _muted = false;
            int baseVol = _preMuteVolume > 0 ? _preMuteVolume : (_volume > 0 ? _volume : 30);
            newVol = Math.Clamp(baseVol + delta, 0, 100);
            SetVolumeInternal(newVol);
        }
        else
        {
            newVol = Math.Clamp(_volume + delta, 0, 100);
            SetVolumeInternal(newVol);
        }
    }

    public bool ToggleMute()
    {
        if (_muted)
        {
            _muted = false;
            int restore = _preMuteVolume > 0 ? _preMuteVolume
                        : _volume > 0 ? _volume
                        : 50;
            CommitVolume(restore);
        }
        else
        {
            if (_volume > 0) _preMuteVolume = _volume;
            _muted = true;
            CommitVolume(0);
        }
        return _muted;
    }

    public void Dispose() => Close();
}