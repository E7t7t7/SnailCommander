using System.Text.Json;

namespace SnailCommander.Core.Updates;

public sealed record ReplacedFile(string RelativePath, bool HadOriginal);
public sealed record UpdateJournal(string Destination, string Status, List<ReplacedFile> Files,
    string? PreviousVersion = null, string? InstalledVersion = null);

/// <summary>A persisted replacement journal allows rollback after copy or startup failure.</summary>
public sealed class UpdateTransaction
{
    private readonly string _backup;
    private UpdateJournal _journal;
    public string BackupDirectory => _backup;
    public string Status => _journal.Status;

    private UpdateTransaction(string backup, UpdateJournal journal) { _backup = backup; _journal = journal; }

    public static UpdateTransaction Load(string backup)
    {
        var journal = JsonSerializer.Deserialize<UpdateJournal>(File.ReadAllText(Path.Combine(backup, "journal.json")))
            ?? throw new InvalidDataException("Missing update journal.");
        if (journal.Files is null || journal.Files.Count > UpdatePolicy.MaxFiles || !Path.IsPathFullyQualified(journal.Destination)
            || journal.Status is not ("prepared" or "installing" or "awaiting-health" or "healthy" or "rolled-back"))
            throw new InvalidDataException("Invalid update journal.");
        foreach (var file in journal.Files) UpdatePolicy.ValidateRelativePath(file.RelativePath);
        return new UpdateTransaction(Path.GetFullPath(backup), journal);
    }

    public static UpdateTransaction Create(string destination, string backup, string? previousVersion = null, string? installedVersion = null)
    {
        EnsureSeparateDirectories(destination, backup);
        RejectLink(destination);
        RejectLink(backup);
        var transaction = new UpdateTransaction(Path.GetFullPath(backup), new UpdateJournal(Path.GetFullPath(destination), "prepared", new(), previousVersion, installedVersion));
        Directory.CreateDirectory(backup);
        transaction.Save();
        return transaction;
    }

    public void Install(string staging, Action<string, string>? copy = null)
    {
        EnsureSeparateDirectories(staging, _journal.Destination);
        EnsureSeparateDirectories(staging, _backup);
        var files = EnumeratePackage(staging); // Bound and validate the entire tree before replacing any files.
        copy ??= (source, target) => File.Copy(source, target, overwrite: true);
        _journal = _journal with { Status = "installing" };
        Save();
        foreach (var file in files)
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Update links are not allowed.");
            var relative = Path.GetRelativePath(staging, file).Replace('\\', '/');
            UpdatePolicy.ValidateRelativePath(relative);
            var target = ResolveInside(_journal.Destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var hadOriginal = File.Exists(target);
            if (hadOriginal)
            {
                var original = ResolveInside(_backup, "files/" + relative);
                Directory.CreateDirectory(Path.GetDirectoryName(original)!);
                File.Copy(target, original, overwrite: false);
            }
            _journal.Files.Add(new ReplacedFile(relative, hadOriginal));
            Save(); // Record rollback intent durably before replacing the destination.
            copy(file, target);
        }
        _journal = _journal with { Status = "awaiting-health" };
        Save();
    }

    public void Commit()
    {
        _journal = _journal with { Status = "healthy" };
        Save();
    }

    public void Rollback()
    {
        var failures = new List<Exception>();
        foreach (var file in _journal.Files.AsEnumerable().Reverse())
        {
            try
            {
                var target = ResolveInside(_journal.Destination, file.RelativePath);
                if (file.HadOriginal) File.Copy(ResolveInside(_backup, "files/" + file.RelativePath), target, true);
                else if (File.Exists(target)) File.Delete(target);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { failures.Add(ex); }
        }
        if (failures.Count > 0) throw new AggregateException("Rollback could not restore every file; preserve this backup for manual recovery.", failures);
        _journal = _journal with { Status = "rolled-back" };
        Save();
    }

    private void Save()
    {
        var path = Path.Combine(_backup, "journal.json");
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(_journal, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(path + ".tmp", path, true);
    }

    private static List<string> EnumeratePackage(string staging)
    {
        RejectLink(staging);
        var result = new List<string>();
        var pending = new Stack<(string Path, int Depth)>();
        pending.Push((Path.GetFullPath(staging), 0));
        long size = 0;
        int entries = 0;
        while (pending.TryPop(out var directory))
        {
            if (directory.Depth > 64) throw new InvalidDataException("Update directory tree is too deep.");
            foreach (var path in Directory.EnumerateFileSystemEntries(directory.Path))
            {
                if (++entries > UpdatePolicy.MaxFiles) throw new InvalidDataException("Too many update entries.");
                RejectLink(path);
                if (Directory.Exists(path)) pending.Push((path, directory.Depth + 1));
                else
                {
                    var length = new FileInfo(path).Length;
                    if (length > UpdatePolicy.MaxExpandedBytes - size) throw new InvalidDataException("Expanded update exceeds its size limit.");
                    size += length;
                    UpdatePolicy.ValidateRelativePath(Path.GetRelativePath(staging, path).Replace('\\', '/'));
                    result.Add(path);
                }
            }
        }
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    private static void EnsureSeparateDirectories(string first, string second)
    {
        var a = Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var b = Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (a.Equals(b, StringComparison.OrdinalIgnoreCase)
            || a.StartsWith(b + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || b.StartsWith(a + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Update directories must not overlap.");
    }

    private static void RejectLink(string path)
    {
        if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Update paths must not traverse symbolic links or junctions.");
    }

    private static string ResolveInside(string root, string relative)
    {
        UpdatePolicy.ValidateRelativePath(relative);
        var resolved = Path.GetFullPath(root);
        RejectLink(resolved);
        foreach (var part in relative.Split('/'))
        {
            resolved = Path.Combine(resolved, part);
            RejectLink(resolved);
        }
        return resolved;
    }

    public static void PruneHealthyBackups(string backupRoot, int retention = 3)
    {
        var resolved = Path.GetFullPath(backupRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var healthy = new List<string>();
        if (!Directory.Exists(backupRoot)) return;
        foreach (var directory in Directory.GetDirectories(backupRoot).OrderByDescending(Path.GetFileName, StringComparer.Ordinal))
        {
            try { if (Load(directory).Status == "healthy") healthy.Add(directory); }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException) { }
        }
        foreach (var directory in healthy.Skip(Math.Clamp(retention, 1, 10)))
        {
            var target = Path.GetFullPath(directory);
            if (!target.StartsWith(resolved, StringComparison.OrdinalIgnoreCase) || (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Unsafe backup retention path.");
            Directory.Delete(target, recursive: true);
        }
    }
}
