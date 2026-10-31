using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ReadDock;

internal sealed record ReadingRecord(string Id, DateTimeOffset CreatedAt, string Original, string Translation, string Direction = "en-zh")
{
    public override string ToString()
    {
        string preview = Regex.Replace(Original, @"\s+", " ").Trim();
        return CreatedAt.ToLocalTime().ToString("MM-dd HH:mm") + "  " + (preview.Length > 44 ? preview[..44] + "…" : preview);
    }
}

internal sealed class ReadingHistory
{
    readonly string file;
    List<ReadingRecord> records = [];
    public IReadOnlyList<ReadingRecord> Records => records.AsReadOnly();
    public string? LoadError { get; private set; }
    public ReadingHistory(string directory)
    {
        file = Path.Combine(directory, "reading-history.dat");
        try { File.Delete(file + ".tmp"); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        if (Directory.Exists(directory))
            foreach (string leftover in Directory.EnumerateFiles(directory, "reading-history.*.tmp"))
                if (Guid.TryParseExact(Path.GetFileName(leftover)[16..^4], "N", out _))
                    try { File.Delete(leftover); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        if (!File.Exists(file)) return;
        try
        {
            var plain = LocalDataProtection.Unprotect(File.ReadAllBytes(file));
            try { records = (JsonSerializer.Deserialize<List<ReadingRecord>>(plain) ?? throw new InvalidDataException()).Select(RedactRecord).ToList(); }
            finally { CryptographicOperations.ZeroMemory(plain); }
        }
        catch (Exception ex) when (ex is JsonException or CryptographicException or IOException or UnauthorizedAccessException)
        { LoadError = "本地历史无法读取，原文件已保留。可清空历史后重新启用。"; }
    }
    public int RemovalCount(int limit) => Math.Max(0, records.Count - Math.Clamp(limit, 0, 1000));
    public void Add(string original, string translation, int limit, string direction = "en-zh")
    {
        if (limit == 0) return;
        EnsureReadable();
        var next = records.Prepend(new ReadingRecord(Guid.NewGuid().ToString("N"), DateTimeOffset.Now, SecretRedactor.Redact(original), SecretRedactor.Redact(translation), direction)).Take(Math.Clamp(limit, 0, 1000)).ToList();
        Commit(next);
    }
    public void Trim(int limit)
    {
        EnsureReadable();
        if (RemovalCount(limit) > 0) Commit(records.Take(Math.Clamp(limit, 0, 1000)).ToList());
    }
    public void Clear() { Commit([]); LoadError = null; }
    void EnsureReadable() { if (LoadError != null) throw new InvalidDataException(LoadError); }
    void Commit(List<ReadingRecord> next)
    {
        next = next.Select(RedactRecord).ToList();
        if (next.Count == 0) { File.Delete(file); records = next; return; }
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(next);
        byte[] encrypted;
        try { encrypted = LocalDataProtection.Protect(plain); }
        finally { CryptographicOperations.ZeroMemory(plain); }
        string temp = Path.Combine(Path.GetDirectoryName(file)!, "reading-history." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllBytes(temp, encrypted);
            // Antivirus/indexers can hold a new encrypted file briefly. Keep the old history intact while retrying the atomic replace.
            for (int attempt = 0; ; attempt++)
            {
                try { File.Move(temp, file, true); break; }
                catch (Exception ex) when (attempt < 4 && ex is IOException or UnauthorizedAccessException) { Thread.Sleep(40 * (attempt + 1)); }
            }
            records = next;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    static ReadingRecord RedactRecord(ReadingRecord record) => record with { Original = SecretRedactor.Redact(record.Original), Translation = SecretRedactor.Redact(record.Translation) };
}

// DPAPI ties history to the current Windows account. No Key or plaintext sidecar is stored.
internal static class LocalDataProtection
{
    [StructLayout(LayoutKind.Sequential)] struct Blob { public int Length; public IntPtr Data; }
    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CryptProtectData(ref Blob input, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
    [DllImport("crypt32.dll", SetLastError = true)]
    static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
    [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr memory);
    public static byte[] Protect(byte[] value) => Transform(value, true);
    public static byte[] Unprotect(byte[] value) => Transform(value, false);
    static byte[] Transform(byte[] value, bool protect)
    {
        var input = new Blob { Length = value.Length, Data = Marshal.AllocHGlobal(value.Length) };
        Blob output = default;
        try
        {
            Marshal.Copy(value, 0, input.Data, value.Length);
            bool ok = protect ? CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output)
                : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!ok) throw new CryptographicException("无法保护或读取本地历史数据。");
            var result = new byte[output.Length]; Marshal.Copy(output.Data, result, 0, result.Length); return result;
        }
        finally
        {
            for (int i = 0; i < input.Length; i++) Marshal.WriteByte(input.Data, i, 0);
            Marshal.FreeHGlobal(input.Data);
            if (output.Data != IntPtr.Zero) { for (int i = 0; i < output.Length; i++) Marshal.WriteByte(output.Data, i, 0); LocalFree(output.Data); }
        }
    }
}
