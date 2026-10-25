using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace SnailCommander;

internal sealed record UpdateInfo(string CommitSha, string DownloadUrl, string ReleaseUrl, string? Sha256Digest);

/// <summary>
/// Downloads public, CI-published GitHub Releases without GitHub credentials.
/// </summary>
internal static class GitHubUpdates
{
    private const string Repo = "E7t7t7/SnailCommander";
    private const string ReleasesApi = "https://api.github.com/repos/" + Repo + "/releases/latest";
    private const string TrustedDownloadPrefix = "https://github.com/" + Repo + "/releases/download/";
    private const long MaxArchiveBytes = 400L * 1024 * 1024;
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(5) };

    public static string CurrentBuildSha
    {
        get
        {
            var info = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
            var separator = info.LastIndexOf('+');
            return separator >= 0 ? info[(separator + 1)..] : "";
        }
    }

    public static async Task<UpdateInfo> LatestAsync(CancellationToken cancellationToken = default)
    {
        using var request = NewPublicRequest(ReleasesApi);
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new InvalidOperationException(
                "No public Snail Commander release is available yet. Check the Releases page for a successful Windows build.");
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var release = doc.RootElement;
        var tagName = release.GetProperty("tag_name").GetString() ?? "";
        const string prefix = "build-";
        if (!tagName.StartsWith(prefix, StringComparison.Ordinal)
            || tagName.Length != prefix.Length + 40
            || !tagName[prefix.Length..].All(Uri.IsHexDigit))
            throw new InvalidDataException("The latest release tag is not a recognized build commit.");

        var sha = tagName[prefix.Length..].ToLowerInvariant();
        var releaseUrl = release.GetProperty("html_url").GetString() ?? "";
        foreach (var item in release.GetProperty("assets").EnumerateArray())
        {
            if (item.GetProperty("name").GetString() != "SnailCommander-win-x64.zip")
                continue;

            var url = item.GetProperty("browser_download_url").GetString() ?? "";
            if (!url.StartsWith(TrustedDownloadPrefix, StringComparison.Ordinal)
                || !Uri.TryCreate(url, UriKind.Absolute, out var assetUri)
                || assetUri.Scheme != Uri.UriSchemeHttps
                || !string.Equals(assetUri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Release download URL is not from the expected GitHub repository.");

            string? digest = null;
            if (item.TryGetProperty("digest", out var digestProperty)
                && digestProperty.ValueKind == JsonValueKind.String)
                digest = digestProperty.GetString();

            if (item.GetProperty("size").GetInt64() > MaxArchiveBytes)
                throw new InvalidDataException("The release package exceeds the 400 MB size limit.");

            return new UpdateInfo(sha, url, releaseUrl, digest);
        }

        throw new InvalidOperationException("The newest public release is missing the Windows ZIP package.");
    }

    public static bool AlreadyInstalled(UpdateInfo update)
        => CurrentBuildSha.Length == 40
           && string.Equals(update.CommitSha, CurrentBuildSha, StringComparison.OrdinalIgnoreCase);

    private static HttpRequestMessage NewPublicRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("SnailCommander-Updater/0.3");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return request;
    }

    public static async Task<string> DownloadAndPrepareAsync(UpdateInfo update, CancellationToken ct = default)
    {
        var workDir = Path.Combine(Path.GetTempPath(), "SnailCommander", "Update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        var archivePath = Path.Combine(workDir, "download.zip");
        var stagingPath = Path.Combine(workDir, "staging");

        using (var request = NewPublicRequest(update.DownloadUrl))
        using (var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long length && length > MaxArchiveBytes)
                throw new InvalidDataException("Update archive exceeds the 400 MB size limit.");

            await using var input = await response.Content.ReadAsStreamAsync(ct);
            await using var output = File.Create(archivePath);
            var buffer = new byte[1024 * 1024];
            long bytes = 0;
            int read;
            while ((read = await input.ReadAsync(buffer.AsMemory(), ct)) > 0)
            {
                bytes += read;
                if (bytes > MaxArchiveBytes)
                    throw new InvalidDataException("Update archive exceeds the 400 MB size limit.");
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
            }
        }

        if (string.IsNullOrWhiteSpace(update.Sha256Digest)
            || !update.Sha256Digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("GitHub did not provide a SHA-256 digest for this release asset.");

        var expectedDigest = update.Sha256Digest["sha256:".Length..];
        if (expectedDigest.Length != 64 || !expectedDigest.All(Uri.IsHexDigit))
            throw new InvalidDataException("GitHub returned an invalid release digest.");

        await using (var file = File.OpenRead(archivePath))
        {
            var actualDigest = Convert.ToHexString(await SHA256.HashDataAsync(file, ct));
            if (!string.Equals(actualDigest, expectedDigest, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The downloaded ZIP failed SHA-256 verification. No files were changed.");
        }

        System.IO.Compression.ZipFile.ExtractToDirectory(archivePath, stagingPath);
        if (!File.Exists(Path.Combine(stagingPath, "SnailCommander.exe"))
            || !File.Exists(Path.Combine(stagingPath, "SnailCommander.Updater.exe")))
            throw new InvalidDataException("The release is missing the Snail Commander application or updater.");

        return stagingPath;
    }

    /// <summary>Copies updater to temp so it can replace the installation's updater file after this app exits.</summary>
    public static void LaunchInstallerAndExit(string stagingPath)
    {
        var worker = Path.Combine(Path.GetDirectoryName(stagingPath)!, "SnailCommander.UpdateWorker.exe");
        File.Copy(Path.Combine(stagingPath, "SnailCommander.Updater.exe"), worker, overwrite: true);
        var info = new ProcessStartInfo(worker)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(worker)!
        };
        info.ArgumentList.Add("--wait-pid");
        info.ArgumentList.Add(Environment.ProcessId.ToString());
        info.ArgumentList.Add("--staging");
        info.ArgumentList.Add(stagingPath);
        info.ArgumentList.Add("--destination");
        info.ArgumentList.Add(AppContext.BaseDirectory);
        Process.Start(info);
    }
}
