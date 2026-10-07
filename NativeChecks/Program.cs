using Native;
using System.Text;
using System.Text.Json;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--make-icon") { MakeIcon(args[1], args[2]); return; }
        if (args.Length > 0 && args[0] == "--hive") { var snapshot = DesktopService.ReadHive(args[1]); Console.WriteLine(JsonSerializer.Serialize(new { Values = snapshot.Values.Count, Items = DesktopService.ParseNames(Convert.FromBase64String(snapshot.Values["IconLayouts"].Data.GetString()!)).Count })); return; }
        var raw = DesktopService.ActiveLayout(); var names = DesktopService.ParseNames(raw);
        if (names.Count != BitConverter.ToUInt32(raw, 24) || names.Any(n => n.Any(char.IsControl))) throw new Exception("Name table validation failed.");
        var unicode = Encoding.Unicode.GetBytes("\u4e2d\u6587.lnk>  \0");
        var fixture = new byte[40 + unicode.Length + 100]; BitConverter.GetBytes(1U).CopyTo(fixture, 24); BitConverter.GetBytes((ulong)(unicode.Length / 2)).CopyTo(fixture, 32); unicode.CopyTo(fixture, 40);
        if (DesktopService.ParseNames(fixture).Single() != "\u4e2d\u6587.lnk") throw new Exception("Valid Unicode name lost.");
        foreach (var invalid in new[] { new byte[8], raw[..60] }) { try { DesktopService.ParseNames(invalid); throw new Exception("Accepted truncated input."); } catch (InvalidDataException) { } }
        int snapshots = 0;
        foreach (var path in DesktopService.Backups()) { var snapshot = DesktopService.LoadSnapshot(path); DesktopService.ParseNames(Convert.FromBase64String(snapshot.Values["IconLayouts"].Data.GetString()!)); snapshots++; }
        string saved = DesktopService.SaveSnapshot("Native migration verification");
        var roundtrip = DesktopService.LoadSnapshot(saved);
        if (!Convert.FromBase64String(roundtrip.Values["IconLayouts"].Data.GetString()!).SequenceEqual(raw)) throw new Exception("Snapshot bytes changed.");
        DesktopService.DeleteSnapshot(saved);
        if (File.Exists(saved)) throw new Exception("Snapshot deletion failed.");
        var desktop = DesktopService.ReadDesktop();
        if (desktop.Items.Count != names.Count || desktop.Items.Count(i => i.Icon != null) < names.Count - 2) throw new Exception("Desktop icon extraction incomplete.");
        var health = DesktopService.Scan();
        var report = new { Passed = true, SavedNames = names.Count, RealItems = desktop.Items.Count, RealIcons = desktop.Items.Count(i => i.Icon != null), LoadedSnapshots = snapshots, Health = health, MalformedDataRejected = true, UnicodePreserved = true, SnapshotBytesPreserved = true, SnapshotDeletionVerified = true };
        File.WriteAllText(Path.Combine(DesktopService.DataDirectory, "backend-verification.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(report));
    }
    static void MakeIcon(string source, string destination)
    {
        using var image = System.Drawing.Image.FromFile(source);
        int[] sizes = [16, 24, 32, 48, 64, 128, 256]; var frames = new List<byte[]>();
        foreach (int size in sizes)
        {
            using var bitmap = new System.Drawing.Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb); using var graphics = System.Drawing.Graphics.FromImage(bitmap);
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic; graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            graphics.DrawImage(image, 0, 0, size, size); using var stream = new MemoryStream(); bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png); frames.Add(stream.ToArray());
        }
        using var output = new BinaryWriter(File.Create(destination)); output.Write((ushort)0); output.Write((ushort)1); output.Write((ushort)sizes.Length); uint offset = (uint)(6 + 16 * sizes.Length);
        for (int index = 0; index < sizes.Length; index++) { output.Write((byte)(sizes[index] == 256 ? 0 : sizes[index])); output.Write((byte)(sizes[index] == 256 ? 0 : sizes[index])); output.Write((byte)0); output.Write((byte)0); output.Write((ushort)1); output.Write((ushort)32); output.Write((uint)frames[index].Length); output.Write(offset); offset += (uint)frames[index].Length; }
        foreach (var frame in frames) output.Write(frame);
        Console.WriteLine("Created seven icon sizes, 16 through 256 pixels.");
    }
}
