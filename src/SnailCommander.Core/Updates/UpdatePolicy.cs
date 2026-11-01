using System.IO.Compression;
using System.Security.Cryptography;

namespace SnailCommander.Core.Updates;

public static class UpdatePolicy
{
    public const long MaxArchiveBytes = 400L * 1024 * 1024;
    public const long MaxExpandedBytes = 1200L * 1024 * 1024;
    public const int MaxFiles = 4096;

    public static string CommitFromInformationalVersion(string informationalVersion)
    {
        int separator = informationalVersion.LastIndexOf('+');
        if (separator < 0) return "";
        // The .NET SDK can append SourceRevisionId even when CI supplies a commit.
        // Accept repeated identical SHA metadata, but never guess between conflicting SHAs.
        var commits = informationalVersion[(separator + 1)..].Split('.')
            .Where(part => part.Length == 40 && part.All(Uri.IsHexDigit))
            .Select(part => part.ToLowerInvariant()).Distinct(StringComparer.Ordinal).ToArray();
        return commits.Length == 1 ? commits[0] : "";
    }

    public static string CommitFromTag(string tag)
    {
        if (!tag.StartsWith("build-", StringComparison.Ordinal) || tag.Length != 46 || !tag[6..].All(Uri.IsHexDigit))
            throw new InvalidDataException("Release tag is not a recognized build commit.");
        return tag[6..].ToLowerInvariant();
    }

    public static bool IsUpdateAvailable(string installedCommit, Version installedVersion, string candidateCommit, Version? candidateVersion)
    {
        if (string.Equals(installedCommit, candidateCommit, StringComparison.OrdinalIgnoreCase)) return false;
        return candidateVersion is null || Normalize(candidateVersion) >= Normalize(installedVersion);
    }

    private static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision));

    public static Version? VersionFromReleaseName(string name)
    {
        // CI names releases "Snail Commander vX.Y.Z (build ...)"; legacy builds have no version.
        foreach (var part in name.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            if (part.StartsWith('v') && Version.TryParse(part[1..], out var version)) return version;
        return null;
    }

    public static async Task VerifyDigestAsync(string archive, string? digest, CancellationToken ct = default)
    {
        if (digest is null || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
            || digest.Length != 71 || !digest[7..].All(Uri.IsHexDigit))
            throw new InvalidDataException("GitHub did not provide a valid SHA-256 release digest.");
        await using var file = File.OpenRead(archive);
        if (file.Length > MaxArchiveBytes) throw new InvalidDataException("Update archive exceeds the 400 MB limit.");
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(file, ct));
        if (!string.Equals(actual, digest[7..], StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The downloaded ZIP failed SHA-256 verification. No application files were changed.");
    }

    public static void ExtractPackage(string archive, string destination)
    {
        using var zip = ZipFile.OpenRead(archive);
        if (zip.Entries.Count > MaxFiles) throw new InvalidDataException("Too many update files.");
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        // Validate every entry before writing anything, including Windows path semantics on non-Windows CI.
        foreach (var entry in zip.Entries)
        {
            var relative = entry.FullName.Replace('\\', '/');
            if (relative.EndsWith('/')) relative = relative[..^1];
            ValidateRelativePath(relative);
            if (!paths.Add(relative)) throw new InvalidDataException("Duplicate update path.");
            if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                throw new InvalidDataException("Symbolic links are not allowed in update packages.");
            if (entry.Length > MaxExpandedBytes - total) throw new InvalidDataException("Expanded update exceeds its size limit.");
            total += entry.Length;
        }
        if (!zip.Entries.Any(e => e.FullName == "SnailCommander.exe") || !zip.Entries.Any(e => e.FullName == "SnailCommander.Updater.exe"))
            throw new InvalidDataException("Release is missing the application or updater executable.");
        Directory.CreateDirectory(destination);
        zip.ExtractToDirectory(destination);
    }

    public static void ValidateRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains(':') || path.Contains('\\'))
            throw new InvalidDataException("Unsafe update file path.");
        foreach (var segment in path.Split('/'))
        {
            if (segment is "" or "." or ".." || segment.EndsWith('.') || segment.EndsWith(' ')
                || segment.Any(c => c < 32 || "<>\"|?*".Contains(c)))
                throw new InvalidDataException("Unsafe update file path.");
            var stem = segment.Split('.')[0].ToUpperInvariant();
            if (stem is "CON" or "PRN" or "AUX" or "NUL" || (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && stem[3] is >= '1' and <= '9'))
                throw new InvalidDataException("Reserved Windows update path.");
        }
    }
}
