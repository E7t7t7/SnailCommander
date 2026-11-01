using System.Text.Json;
using System.Text.Json.Serialization;

namespace SnailCommander.Core.Observation;

/// <summary>
/// Atomic immutable observation files. Each committed record survives process restarts independently;
/// corrupt/incomplete records are skipped and retention bounds the directory. No image data is stored.
/// </summary>
public sealed class ObservationStore : IObservationStore
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true, PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly string _directory;
    private readonly int _retainedCount;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private const long MaximumRecordBytes = 512 * 1024;

    public ObservationStore(string directory, int retainedCount = 3000)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (retainedCount is < 1 or > 100_000) throw new ArgumentOutOfRangeException(nameof(retainedCount));
        _directory = Path.GetFullPath(directory);
        _retainedCount = retainedCount;
    }

    public async Task SaveAsync(ScreenObservation observation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (!IsValid(observation)) throw new ArgumentException("Observation contains invalid dimensions, text, or structured recognition data.");
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(observation, JsonOptions);
        if (json.Length > MaximumRecordBytes) throw new InvalidDataException("Observation exceeds the 512 KB record limit.");
        await _writeGate.WaitAsync(cancellationToken);
        string? temporary = null;
        try
        {
            Directory.CreateDirectory(_directory);
            // Independent app instances must not race the retention limit. The lock file is never deleted.
            await using var directoryLock = await AcquireDirectoryLockAsync(cancellationToken);
            // Clear any previous incomplete cleanup before writing; keep existing good records until
            // the new record is committed. Even if cleanup is denied, growth is bounded to limit + 1.
            foreach (var old in Directory.EnumerateFiles(_directory, "*.json").OrderByDescending(Path.GetFileName).Skip(_retainedCount))
                File.Delete(old);
            var path = Path.Combine(_directory, observation.CapturedAt.UtcDateTime.ToString("yyyyMMdd-HHmmss-fffffff")
                + "-" + Guid.NewGuid().ToString("N") + ".json");
            temporary = path + ".tmp";
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await file.WriteAsync(json, cancellationToken);
                await file.FlushAsync(cancellationToken);
                file.Flush(flushToDisk: true);
            }
            File.Move(temporary, path);
            temporary = null;
            try
            {
                foreach (var old in Directory.EnumerateFiles(_directory, "*.json").OrderByDescending(Path.GetFileName).Skip(_retainedCount))
                    File.Delete(old);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The committed record remains valid. The next writer must clear the extra record
                // before adding another, so repeated cleanup failures cannot accumulate history.
            }
            // A crash can leave only an uncommitted .tmp file, which cannot masquerade as history.
            try
            {
                foreach (var old in Directory.EnumerateFiles(_directory, "*.tmp").Where(p => File.GetLastWriteTimeUtc(p) < DateTime.UtcNow.AddDays(-1)))
                    File.Delete(old);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        finally
        {
            try { if (temporary is not null && File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            _writeGate.Release();
        }
    }

    public IReadOnlyList<ScreenObservation> LoadRecent(int count = 100)
    {
        if (count is < 1 or > 100_000) throw new ArgumentOutOfRangeException(nameof(count));
        if (!Directory.Exists(_directory)) return Array.Empty<ScreenObservation>();
        var records = new List<ScreenObservation>();
        foreach (var path in Directory.EnumerateFiles(_directory, "*.json").OrderByDescending(Path.GetFileName))
        {
            try
            {
                if (new FileInfo(path).Length > MaximumRecordBytes) continue;
                var item = JsonSerializer.Deserialize<ScreenObservation>(File.ReadAllText(path), JsonOptions);
                if (item is not null && IsValid(item))
                    records.Add(item);
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
            if (records.Count >= count) break;
        }
        return records.OrderByDescending(r => r.CapturedAt).ToArray();
    }

    private async Task<FileStream> AcquireDirectoryLockAsync(CancellationToken cancellationToken)
    {
        for (int attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { return new FileStream(Path.Combine(_directory, ".write.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (attempt < 40) { await Task.Delay(50, cancellationToken); }
        }
    }

    private static bool IsValid(ScreenObservation item) =>
        item.Width is > 0 and <= 8192 && item.Height is > 0 and <= 8192
        && !string.IsNullOrWhiteSpace(item.Id) && item.Device is not null && item.Text is not null
        && item.Classification is not null && Enum.IsDefined(item.Classification.Kind)
        && double.IsFinite(item.Classification.Confidence) && item.Classification.Confidence is >= 0 and <= 1
        && item.Classification.Evidence is not null && item.Classification.Evidence.All(e => e is not null)
        && item.Elements is not null && item.Elements.All(e => e is not null && e.Text is not null
            && double.IsFinite(e.X) && double.IsFinite(e.Y) && double.IsFinite(e.Width) && double.IsFinite(e.Height)
            && e.X >= 0 && e.Y >= 0 && e.Width >= 0 && e.Height >= 0)
        && item.Resources is not null && item.Resources.All(r => r is not null && !string.IsNullOrWhiteSpace(r.Name)
            && r.Quantity >= 0 && r.RawText is not null);
}

public static class ObservationReplay
{
    /// <summary>Re-evaluates recorded OCR text through the current rules without contacting an emulator.</summary>
    public static IReadOnlyList<ScreenObservation> Run(IEnumerable<ScreenObservation> records, IScreenClassifier classifier)
        => records.OrderBy(r => r.CapturedAt).Select(r => r with
        {
            Classification = classifier.Classify(r.Text), Resources = ResourceTextParser.Parse(r.Text),
            RecognitionSource = "Replay of recorded OCR text; no new image recognition"
        }).ToArray();
}
