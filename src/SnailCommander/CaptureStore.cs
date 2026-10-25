using System.Text.Json;

namespace SnailCommander;

internal sealed record CaptureRecord(
    string FileName,
    DateTimeOffset CapturedAt,
    string Device,
    int Width,
    int Height,
    string Label)
{
    public string Dimensions => $"{Width} × {Height}";
}

internal sealed class CaptureStore
{
    public static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        "SnailCommander", "Captures");

    public static readonly string[] Labels =
    {
        "Unclassified", "Home / base", "Story / memory", "Exploration",
        "Battle", "Equipment", "Relics", "Research", "Event", "Other"
    };

    private static readonly JsonSerializerOptions JsonOptions =
        new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public static async Task<CaptureRecord> SaveAsync(byte[] png, string device)
    {
        Directory.CreateDirectory(DirectoryPath);
        string file = $"snail-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.png";
        string path = Path.Combine(DirectoryPath, file);
        await File.WriteAllBytesAsync(path, png);
        try
        {
            using var memory = new MemoryStream(png, writable: false);
            using var image = Image.FromStream(memory);
            var result = new CaptureRecord(file, DateTimeOffset.Now, device,
                image.Width, image.Height, "Unclassified");
            await WriteMetadataAsync(result);
            return result;
        }
        catch
        {
            // A failed capture should not leave an orphaned, invalid PNG file.
            try { File.Delete(path); } catch { }
            throw;
        }
    }

    public static string ImagePath(CaptureRecord record)
        => Path.Combine(DirectoryPath, Path.GetFileName(record.FileName));

    public static async Task SaveLabelAsync(CaptureRecord record, string label)
    {
        if (!Labels.Contains(label))
            throw new ArgumentException("Unknown screen label.");
        await WriteMetadataAsync(record with { Label = label });
    }

    private static async Task WriteMetadataAsync(CaptureRecord record)
    {
        Directory.CreateDirectory(DirectoryPath);
        var path = Path.Combine(DirectoryPath, Path.ChangeExtension(Path.GetFileName(record.FileName), ".json"));
        var temp = path + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(record, JsonOptions));
        File.Move(temp, path, overwrite: true);
    }

    public static List<CaptureRecord> LoadRecent(int limit = 300)
    {
        Directory.CreateDirectory(DirectoryPath);
        var captures = new List<CaptureRecord>();
        foreach (var path in Directory.EnumerateFiles(DirectoryPath, "*.png")
                     .OrderByDescending(File.GetLastWriteTimeUtc).Take(limit))
        {
            var fileName = Path.GetFileName(path);
            try
            {
                var jsonPath = Path.ChangeExtension(path, ".json");
                if (File.Exists(jsonPath))
                {
                    var record = JsonSerializer.Deserialize<CaptureRecord>(File.ReadAllText(jsonPath), JsonOptions);
                    if (record is not null && Path.GetFileName(record.FileName) == fileName)
                    {
                        captures.Add(record);
                        continue;
                    }
                }

                using var img = Image.FromFile(path);
                captures.Add(new CaptureRecord(fileName, File.GetLastWriteTime(path), "Unknown",
                    img.Width, img.Height, "Unclassified"));
            }
            catch (Exception) { /* Skip unreadable captures, continue showing healthy ones. */ }
        }
        return captures.OrderByDescending(x => x.CapturedAt).ToList();
    }
}

internal sealed class LocalPreferences
{
    public string AdbPath { get; set; } = "";
    public string Endpoint { get; set; } = "127.0.0.1:5555";
    public string PreferredDevice { get; set; } = "";
    public bool LivePreviewEnabled { get; set; } = true;
    public int LiveIntervalSeconds { get; set; } = 2;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SnailCommander", "settings.json");

    public static LocalPreferences Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<LocalPreferences>(File.ReadAllText(FilePath)) ?? new()
                : new();
        }
        catch { return new(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, FilePath, overwrite: true);
    }
}
