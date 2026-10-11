using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public static class SerialScaleReader
{
    public static List<string> Ports()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM");
        return key?.GetValueNames().Select(n => key.GetValue(n)?.ToString() ?? "").Where(n => n.Length > 0).Distinct().OrderBy(n => n).ToList() ?? [];
    }
    public static Task<decimal> ReadAsync(ScaleProfile profile) => Task.Run(() =>
    {
        using SafeFileHandle handle = CreateFile(@"\\.\" + profile.Port, 0xC0000000, 0, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (handle.IsInvalid) throw new InvalidOperationException($"Não foi possível abrir {profile.Port}. Confira a conexão e se outra aplicação está usando a porta. {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
        var config = new Dcb { Length = (uint)Marshal.SizeOf<Dcb>() };
        if (!GetCommState(handle, ref config)) Fail();
        config.BaudRate = (uint)profile.BaudRate; config.ByteSize = 8; config.Parity = 0; config.StopBits = 0; config.Flags = 0x1011;
        if (!SetCommState(handle, ref config)) Fail();
        var timeouts = new CommTimeouts { ReadInterval = uint.MaxValue, ReadConstant = 250, WriteConstant = 500 };
        if (!SetCommTimeouts(handle, ref timeouts)) Fail();
        if (profile.CommandHex.Length > 0)
        {
            byte[] request = Convert.FromHexString(profile.CommandHex);
            if (!WriteFile(handle, request, (uint)request.Length, out uint written, IntPtr.Zero) || written != request.Length) Fail();
        }
        var received = new StringBuilder(); byte[] buffer = new byte[512]; var deadline = DateTime.UtcNow.AddSeconds(4);
        while (DateTime.UtcNow < deadline && received.Length < 4096)
        {
            if (!ReadFile(handle, buffer, (uint)buffer.Length, out uint count, IntPtr.Zero)) Fail();
            if (count > 0)
            {
                received.Append(Encoding.ASCII.GetString(buffer, 0, (int)count));
                string text = received.ToString();
                int end = text.IndexOfAny(['\r', '\n', '\u0003']);
                if (end >= 0) return ScaleProtocol.Parse(text[..end], profile.Protocol);
            }
            else if (received.Length > 0) return ScaleProtocol.Parse(received.ToString(), profile.Protocol);
        }
        throw new InvalidOperationException("A balança não respondeu. Confira porta, velocidade, protocolo e comando de solicitação.");
    });
    private static void Fail() => throw new Win32Exception(Marshal.GetLastWin32Error());
    [StructLayout(LayoutKind.Sequential)] private struct Dcb { public uint Length, BaudRate, Flags; public ushort Reserved, XonLimit, XoffLimit; public byte ByteSize, Parity, StopBits; public sbyte XonChar, XoffChar, ErrorChar, EofChar, EvtChar; public ushort Reserved1; }
    [StructLayout(LayoutKind.Sequential)] private struct CommTimeouts { public uint ReadInterval, ReadMultiplier, ReadConstant, WriteMultiplier, WriteConstant; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetCommState(SafeFileHandle handle, ref Dcb dcb);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetCommState(SafeFileHandle handle, ref Dcb dcb);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetCommTimeouts(SafeFileHandle handle, ref CommTimeouts timeouts);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ReadFile(SafeFileHandle handle, byte[] buffer, uint count, out uint read, IntPtr overlapped);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool WriteFile(SafeFileHandle handle, byte[] buffer, uint count, out uint written, IntPtr overlapped);
}
