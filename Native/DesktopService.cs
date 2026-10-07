using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.Drawing;
using System.Drawing.Imaging;

namespace Native;
public record DesktopItem(string Name, string Path, int X, int Y, byte[]? Icon);
public record HealthItem(string Name, string Path, string Status, string? Source);
public record RegistryValue(int Type, JsonElement Data);
public record Snapshot(string Timestamp, string Note, Dictionary<string, RegistryValue> Values);

public static class DesktopService
{
    public const string Bag = @"Software\Microsoft\Windows\Shell\Bags\1\Desktop";
    public static string UserDesktop => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    public static string PublicDesktop => Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
    public static bool IsPortable => File.Exists(System.IO.Path.Combine(AppContext.BaseDirectory, "portable.flag"));
    public static string DataDirectory => IsPortable ? System.IO.Path.Combine(AppContext.BaseDirectory, "Data") : System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopLayoutManager");
    public static string LegacyBackups => System.IO.Path.Combine(AppContext.BaseDirectory, "backups");
    static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, WriteIndented = true, PropertyNameCaseInsensitive = true };
    public static List<string> DesktopFiles() => new[] { UserDesktop, PublicDesktop }.Where(Directory.Exists).SelectMany(Directory.EnumerateFileSystemEntries).ToList();

    public static List<string> ParseNames(byte[] data)
    {
        // The header bounds the string table. Subsequent bytes are layout records, never names.
        if (data.Length < 32) throw new InvalidDataException("The saved layout is incomplete.");
        uint count = BitConverter.ToUInt32(data, 24);
        if (count > 10000) throw new InvalidDataException("This layout format is not supported.");
        int pos = 32; var names = new List<string>();
        for (int i = 0; i < count; i++)
        {
            if (pos + 8 > data.Length) throw new InvalidDataException("Truncated desktop name table.");
            ulong length = BitConverter.ToUInt64(data, pos);
            if (length < 1 || length > 1024 || length * 2 > (ulong)(data.Length - pos - 8)) throw new InvalidDataException("Invalid desktop name table.");
            var text = Encoding.Unicode.GetString(data, pos + 8, (int)length * 2);
            int separator = text.LastIndexOf('>');
            if (separator <= 0) throw new InvalidDataException("A saved desktop name is invalid.");
            var name = text[..separator];
            if (name.Any(char.IsControl)) throw new InvalidDataException("A saved name contains binary data.");
            names.Add(name); pos += 8 + (int)length * 2;
        }
        return names;
    }
    public static byte[] ActiveLayout()
    {
        using var key = Registry.CurrentUser.OpenSubKey(Bag);
        return key?.GetValue("IconLayouts") as byte[] ?? throw new InvalidOperationException("Windows has not saved a desktop layout yet.");
    }
    public static List<HealthItem> Scan()
    {
        var names = ParseNames(ActiveLayout()); var files = DesktopFiles();
        var existing = files.Select(System.IO.Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = names.Distinct(StringComparer.OrdinalIgnoreCase).Where(n => !n.StartsWith("::{") && !existing.Contains(n)).Select(n => new HealthItem(n, "", "Missing from desktop", FindSource(n))).ToList();
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        try
        {
            foreach (var file in files.Where(p => p.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)))
            {
                dynamic shortcut = shell.CreateShortcut(file);
                try
                {
                    string target = Environment.ExpandEnvironmentVariables((string)shortcut.TargetPath);
                    // Empty targets may be advertised or shell shortcuts, not missing files.
                    if (target.Length > 0 && System.IO.Path.IsPathFullyQualified(target) && !File.Exists(target) && !Directory.Exists(target))
                        result.Add(new(System.IO.Path.GetFileName(file), file, $"Target not found: {target}", FindSource(System.IO.Path.GetFileName(file))));
                }
                finally { Marshal.FinalReleaseComObject(shortcut); }
            }
        }
        finally { Marshal.FinalReleaseComObject(shell); }
        return result;
    }
    static string? FindSource(string name)
    {
        foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu) })
        {
            if (!Directory.Exists(root)) continue;
            try { var match = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).FirstOrDefault(p => string.Equals(System.IO.Path.GetFileName(p), name, StringComparison.OrdinalIgnoreCase) && UsableSource(p)); if (match != null) return match; }
            catch (UnauthorizedAccessException) { }
        }
        return null;
    }
    static bool UsableSource(string path)
    {
        if (!path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) return File.Exists(path);
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!; dynamic shortcut = shell.CreateShortcut(path);
        try { string target = Environment.ExpandEnvironmentVariables((string)shortcut.TargetPath); return target.Length > 0 && (File.Exists(target) || Directory.Exists(target)); }
        finally { Marshal.FinalReleaseComObject(shortcut); Marshal.FinalReleaseComObject(shell); }
    }
    public static void RestoreShortcut(HealthItem item, string source)
    {
        if ((!File.Exists(source) && !Directory.Exists(source)) || (source.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) && !UsableSource(source))) throw new FileNotFoundException("The selected source or its shortcut target no longer exists.");
        string name = System.IO.Path.GetFileName(item.Name);
        if (name != item.Name || string.IsNullOrWhiteSpace(name)) throw new InvalidDataException("Invalid shortcut name.");
        var dest = item.Path.Length > 0 ? item.Path : System.IO.Path.Combine(UserDesktop, name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".url", StringComparison.OrdinalIgnoreCase) ? name : name + ".lnk");
        if (File.Exists(dest))
        {
            Directory.CreateDirectory(System.IO.Path.Combine(DataDirectory, "shortcut-backups"));
            File.Copy(dest, System.IO.Path.Combine(DataDirectory, "shortcut-backups", DateTime.Now.ToString("yyyyMMdd_HHmmss_ffff") + "_" + System.IO.Path.GetFileName(dest)));
        }
        if (source.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) || source.EndsWith(".url", StringComparison.OrdinalIgnoreCase)) File.Copy(source, dest, true);
        else
        {
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!; dynamic shortcut = shell.CreateShortcut(dest);
            try { shortcut.TargetPath = source; shortcut.WorkingDirectory = System.IO.Path.GetDirectoryName(source); shortcut.Save(); }
            finally { Marshal.FinalReleaseComObject(shortcut); Marshal.FinalReleaseComObject(shell); }
        }
        if (!File.Exists(dest)) throw new IOException("Windows did not create the shortcut.");
    }
    public static Snapshot ReadRegistry(RegistryKey key, string note)
    {
        var values = new Dictionary<string, RegistryValue>();
        foreach (var name in key.GetValueNames())
        {
            var kind = key.GetValueKind(name); var value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            values[name] = new((int)kind, JsonSerializer.SerializeToElement(value is byte[] bytes ? Convert.ToBase64String(bytes) : value));
        }
        return new(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), note, values);
    }
    public static string SaveSnapshot(string note = "Manual snapshot")
    {
        using var key = Registry.CurrentUser.OpenSubKey(Bag) ?? throw new InvalidOperationException("No saved layout found.");
        Directory.CreateDirectory(System.IO.Path.Combine(DataDirectory, "backups"));
        var path = System.IO.Path.Combine(DataDirectory, "backups", "desktop_backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_ffff") + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(ReadRegistry(key, note), Options)); return path;
    }
    public static List<string> Backups() => new[] { System.IO.Path.Combine(DataDirectory, "backups"), LegacyBackups }.Where(Directory.Exists).SelectMany(p => Directory.EnumerateFiles(p, "*.json")).OrderByDescending(File.GetLastWriteTime).ToList();
    public static Snapshot LoadSnapshot(string path) => JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(path), Options) ?? throw new InvalidDataException("Empty snapshot.");
    public static void DeleteSnapshot(string path)
    {
        var fullPath = System.IO.Path.GetFullPath(path);
        if (!Backups().Any(p => string.Equals(System.IO.Path.GetFullPath(p), fullPath, StringComparison.OrdinalIgnoreCase))) throw new IOException("This file is not a saved layout snapshot.");
        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(fullPath, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin, Microsoft.VisualBasic.FileIO.UICancelOption.ThrowException);
        if (File.Exists(fullPath)) throw new IOException("Windows did not remove the snapshot.");
    }
    public static Snapshot ReadHive(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("The selected registry file was not found. Choose a saved NTUSER.DAT file.", path);
        int error = RegLoadAppKey(path, out var handle, 0x20019, 0, 0);
        if (error != 0) throw new IOException(error switch { 32 => "This registry file is in use. Choose an offline copy, rather than the active Windows profile file.", 5 => "Windows denied access to this registry file. Choose a readable offline copy.", 1009 or 1015 => "This file is not a readable Windows registry hive.", _ => "Windows could not open the registry file: " + new System.ComponentModel.Win32Exception(error).Message });
        using var root = RegistryKey.FromHandle(new SafeRegistryHandle(handle, true));
        using var key = root.OpenSubKey(Bag) ?? throw new InvalidDataException("This file has no saved desktop layout.");
        return ReadRegistry(key, "Offline registry: " + System.IO.Path.GetFileName(path));
    }
    public static void RestoreSnapshot(Snapshot snapshot)
    {
        if (!snapshot.Values.TryGetValue("IconLayouts", out var layout)) throw new InvalidDataException("This snapshot has no icon layout.");
        ParseNames(Convert.FromBase64String(layout.Data.GetString()!));
        var decoded = snapshot.Values.Select(pair => (pair.Key, Kind: (RegistryValueKind)pair.Value.Type, Value: Decode(pair.Value))).ToList();
        SaveSnapshot("Before layout restore");
        RestartExplorer(() => { using var key = Registry.CurrentUser.CreateSubKey(Bag); foreach (var value in decoded) key.SetValue(value.Key, value.Value, value.Kind); });
    }
    static object Decode(RegistryValue value) => (RegistryValueKind)value.Type switch
    {
        RegistryValueKind.Binary => Convert.FromBase64String(value.Data.GetString()!),
        RegistryValueKind.DWord => value.Data.GetInt32(), RegistryValueKind.QWord => value.Data.GetInt64(),
        RegistryValueKind.MultiString => value.Data.Deserialize<string[]>()!,
        RegistryValueKind.String or RegistryValueKind.ExpandString => value.Data.GetString()!,
        _ => throw new InvalidDataException("Unsupported registry value type.")
    };
    public static bool AutoArrange() { using var key = Registry.CurrentUser.OpenSubKey(Bag); return ((int)(key?.GetValue("FFlags") ?? 0) & 1) != 0; }
    public static void DisableAutoArrange()
    {
        SaveSnapshot("Before arrangement change");
        RestartExplorer(() => { using var key = Registry.CurrentUser.CreateSubKey(Bag); int flags = (int)(key.GetValue("FFlags") ?? 0); key.SetValue("FFlags", (flags & ~1) | 4, RegistryValueKind.DWord); key.DeleteValue("SortColumns", false); });
    }
    static void RestartExplorer(Action action)
    {
        foreach (var process in Process.GetProcessesByName("explorer").Where(p => p.SessionId == Process.GetCurrentProcess().SessionId)) { process.Kill(); process.WaitForExit(5000); }
        try { action(); } finally { Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true }); }
        Thread.Sleep(2000);
    }

    public static (List<DesktopItem> Items, int Width, int Height) ReadDesktop()
    {
        nint list = 0;
        EnumWindows((h, _) => { var def = FindWindowEx(h, 0, "SHELLDLL_DefView", null); if (def != 0) list = FindWindowEx(def, 0, "SysListView32", null); return list == 0; }, 0);
        if (list == 0) throw new InvalidOperationException("Desktop icons are hidden or Explorer is unavailable. Show desktop icons and refresh.");
        GetWindowThreadProcessId(list, out uint pid); var process = OpenProcess(0x38, false, pid);
        if (process == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        nint memory = VirtualAllocEx(process, 0, 4096, 0x3000, 4);
        if (memory == 0) { CloseHandle(process); throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()); }
        var result = new List<DesktopItem>();
        var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var file in DesktopFiles())
            {
                paths[System.IO.Path.GetFileName(file)] = file;
                if (file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".url", StringComparison.OrdinalIgnoreCase)) paths[System.IO.Path.GetFileNameWithoutExtension(file)] = file;
                if (SHGetFileInfoPath(file, 0, out var fileInfo, (uint)Marshal.SizeOf<ShellInfo>(), 0x200) != 0) paths[fileInfo.DisplayName ?? ""] = file;
            }
            foreach (var id in new[] { "::{645FF040-5081-101B-9F08-00AA002F954E}", "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}", "::{26EE0668-A00A-44D7-9371-BEB064C98683}" })
            {
                nint pidl = 0; uint attributes = 0;
                try { if (SHParseDisplayName(id, 0, out pidl, 0, ref attributes) == 0 && SHGetFileInfo(pidl, 0, out var info, (uint)Marshal.SizeOf<ShellInfo>(), 0x208) != 0) paths[info.DisplayName] = id; }
                finally { if (pidl != 0) Marshal.FreeCoTaskMem(pidl); }
            }
            int count = (int)Send(list, 0x1004, 0, 0);
            for (int i = 0; i < count; i++)
            {
                // List-view messages require a buffer owned by Explorer, not a pointer into this app.
                var structure = new byte[88]; BitConverter.GetBytes((long)(memory + 128)).CopyTo(structure, 24); BitConverter.GetBytes(1024).CopyTo(structure, 32);
                if (!WriteProcessMemory(process, memory, structure, structure.Length, out _)) throw new IOException("Could not request desktop names.");
                int length = (int)Send(list, 0x1073, i, memory); var text = new byte[Math.Clamp(length, 0, 1023) * 2];
                if (!ReadProcessMemory(process, memory + 128, text, text.Length, out _)) throw new IOException("Could not read desktop names.");
                string name = Encoding.Unicode.GetString(text);
                if (Send(list, 0x1010, i, memory) == 0) throw new IOException("Could not read desktop positions.");
                var point = new byte[8]; ReadProcessMemory(process, memory, point, 8, out _);
                paths.TryGetValue(name, out string? path); path ??= "";
                if (name.Length == 0 && path.Length > 0) name = System.IO.Path.GetFileName(path).Split(".{", 2)[0];
                result.Add(new(name, path, BitConverter.ToInt32(point), BitConverter.ToInt32(point, 4), IconBytes(path)));
            }
            GetClientRect(list, out var rect); return (result, rect.Right, rect.Bottom);
        }
        finally { VirtualFreeEx(process, memory, 0, 0x8000); CloseHandle(process); }
    }
    static nint Send(nint window, uint message, nint index, nint data)
    {
        if (SendMessageTimeout(window, message, index, data, 2, 2000, out nint result) == 0) throw new TimeoutException("Explorer did not respond. Refresh after it recovers.");
        return result;
    }
    public static byte[]? IconBytes(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        nint pidl = 0; uint attrs = 0;
        try
        {
            if (SHParseDisplayName(path, 0, out pidl, 0, ref attrs) != 0) return null;
            if (SHGetFileInfo(pidl, 0, out var info, (uint)Marshal.SizeOf<ShellInfo>(), 0x108) == 0 || info.Icon == 0) return null;
            try { using var icon = Icon.FromHandle(info.Icon); using var bitmap = icon.ToBitmap(); using var stream = new MemoryStream(); bitmap.Save(stream, ImageFormat.Png); return stream.ToArray(); }
            finally { DestroyIcon(info.Icon); }
        }
        catch { return null; }
        finally { if (pidl != 0) Marshal.FreeCoTaskMem(pidl); }
    }
    delegate bool WindowCallback(nint hwnd, nint arg);
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct ShellInfo { public nint Icon; public int Index; public uint Attributes; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName; }
    [DllImport("user32.dll")] static extern bool EnumWindows(WindowCallback callback, nint arg);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern nint FindWindowEx(nint parent, nint after, string cls, string? title);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32.dll")] static extern bool GetClientRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern nint SendMessageTimeout(nint hwnd, uint message, nint wParam, nint lParam, uint flags, uint timeout, out nint result);
    [DllImport("kernel32.dll", SetLastError = true)] static extern nint OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll", SetLastError = true)] static extern nint VirtualAllocEx(nint process, nint address, nuint size, uint allocation, uint protection);
    [DllImport("kernel32.dll")] static extern bool VirtualFreeEx(nint process, nint address, nuint size, uint free);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(nint process, nint address, byte[] buffer, int length, out nuint read);
    [DllImport("kernel32.dll")] static extern bool WriteProcessMemory(nint process, nint address, byte[] buffer, int length, out nuint written);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHParseDisplayName(string name, nint bind, out nint pidl, uint attrs, ref uint outAttrs);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern nint SHGetFileInfo(nint pidl, uint attributes, out ShellInfo info, uint size, uint flags);
    [DllImport("shell32.dll", EntryPoint = "SHGetFileInfoW", CharSet = CharSet.Unicode)] static extern nint SHGetFileInfoPath(string path, uint attributes, out ShellInfo info, uint size, uint flags);
    [DllImport("user32.dll")] static extern bool DestroyIcon(nint icon);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] static extern int RegLoadAppKey(string file, out nint key, uint access, uint options, uint reserved);
}
