using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace GiroBebidas.Core;

/// <summary>DPAPI scoped to the signed-in Windows user. Never stores a plaintext PFX, password or CSC.</summary>
public static class WindowsSecretProtector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob { public int Size; public IntPtr Data; }
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref DataBlob input, string? description, ref DataBlob entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);
    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref DataBlob input, IntPtr description, ref DataBlob entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);

    public static string ProtectString(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        try { return Protect(bytes); } finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    public static string UnprotectString(string value)
    {
        byte[] bytes = Unprotect(value);
        try { return Encoding.UTF8.GetString(bytes); } finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    public static string Protect(byte[] bytes) => Convert.ToBase64String(Transform(bytes, true));
    public static byte[] Unprotect(string value)
    {
        try { return Transform(Convert.FromBase64String(value), false); }
        catch (FormatException) { throw new InvalidOperationException("Credencial fiscal inválida. Cadastre-a novamente."); }
    }
    private static byte[] Transform(byte[] bytes, bool protect)
    {
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("A proteção fiscal exige o Windows.");
        if (bytes.Length > 2_000_000) throw new InvalidOperationException("Credencial fiscal muito grande.");
        var input = Allocate(bytes); var entropy = Allocate(Encoding.UTF8.GetBytes("GiroBebidas.Fiscal.v1")); DataBlob output = default;
        try
        {
            bool ok = protect ? CryptProtectData(ref input, null, ref entropy, IntPtr.Zero, IntPtr.Zero, 1, out output)
                : CryptUnprotectData(ref input, IntPtr.Zero, ref entropy, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!ok) throw new InvalidOperationException(protect ? "O Windows não conseguiu proteger a credencial fiscal."
                : "Credencial fiscal inacessível neste usuário do Windows. Reimporte o certificado e cadastre o CSC novamente.");
            byte[] result = new byte[output.Size]; Marshal.Copy(output.Data, result, 0, result.Length); return result;
        }
        finally
        {
            Clear(input); Clear(entropy);
            if (output.Data != IntPtr.Zero) { for (int i = 0; i < output.Size; i++) Marshal.WriteByte(output.Data, i, 0); LocalFree(output.Data); }
        }
    }
    private static DataBlob Allocate(byte[] value)
    {
        IntPtr memory = Marshal.AllocHGlobal(Math.Max(1, value.Length)); Marshal.Copy(value, 0, memory, value.Length);
        return new() { Size = value.Length, Data = memory };
    }
    private static void Clear(DataBlob blob)
    {
        for (int i = 0; i < blob.Size; i++) Marshal.WriteByte(blob.Data, i, 0);
        Marshal.FreeHGlobal(blob.Data);
    }
}
