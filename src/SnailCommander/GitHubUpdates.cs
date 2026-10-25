using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace SnailCommander;

internal sealed record UpdateInfo(long RunId, long ArtifactId, string CommitSha, string RunUrl);

/// <summary>Read-only access to build artifacts from the owner's private GitHub Actions workflow.</summary>
internal static class GitHubUpdates
{
    private const string Repo = "E7t7t7/SnailCommander";
    private const string ApiBase = "https://api.github.com/repos/" + Repo;
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

    public static async Task<UpdateInfo> LatestAsync(string token, CancellationToken cancellationToken = default)
    {
        using var runs = await ReadJsonAsync(
            $"{ApiBase}/actions/workflows/windows-build.yml/runs?branch=main&status=success&per_page=1",
            token, cancellationToken);
        var runList = runs.RootElement.GetProperty("workflow_runs");
        if (runList.GetArrayLength() == 0)
            throw new InvalidOperationException("No successful Windows builds were found on GitHub.");

        var run = runList[0];
        var runId = run.GetProperty("id").GetInt64();
        var sha = run.GetProperty("head_sha").GetString()
                  ?? throw new InvalidDataException("GitHub run has no commit SHA.");
        var url = run.GetProperty("html_url").GetString() ?? "";

        using var artifacts = await ReadJsonAsync(
            $"{ApiBase}/actions/runs/{runId}/artifacts?per_page=100",
            token, cancellationToken);
        foreach (var item in artifacts.RootElement.GetProperty("artifacts").EnumerateArray())
        {
            if (item.GetProperty("name").GetString() == "SnailCommander-win-x64"
                && !item.GetProperty("expired").GetBoolean())
            {
                return new UpdateInfo(runId, item.GetProperty("id").GetInt64(), sha, url);
            }
        }
        throw new InvalidOperationException("The newest successful build has no downloadable Windows artifact.");
    }

    public static bool AlreadyInstalled(UpdateInfo update)
        => CurrentBuildSha.Length == 40
           && string.Equals(update.CommitSha, CurrentBuildSha, StringComparison.OrdinalIgnoreCase);

    private static HttpRequestMessage Request(string url, string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Configure a GitHub read-only token before checking updates.");

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("SnailCommander-Updater/0.2");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return request;
    }

    private static async Task<JsonDocument> ReadJsonAsync(string url, string token, CancellationToken ct)
    {
        using var request = Request(url, token);
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            if ((int)response.StatusCode is 401 or 403 or 404)
                throw new InvalidOperationException(
                    $"GitHub returned {(int)response.StatusCode}. Check that your token can access the private SnailCommander repository and has Actions: Read permission.");
            response.EnsureSuccessStatusCode();
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    public static async Task<string> DownloadAndPrepareAsync(UpdateInfo update, string token, CancellationToken ct = default)
    {
        var workDir = Path.Combine(Path.GetTempPath(), "SnailCommander", "Update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        var archivePath = Path.Combine(workDir, "download.zip");
        var stagingPath = Path.Combine(workDir, "staging");
        var downloadUrl = $"{ApiBase}/actions/artifacts/{update.ArtifactId}/zip";

        using (var request = Request(downloadUrl, token))
        using (var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode is 401 or 403 or 404)
                    throw new InvalidOperationException($"GitHub download failed ({(int)response.StatusCode}). Check token Actions: Read permission.");
                response.EnsureSuccessStatusCode();
            }
            if (response.Content.Headers.ContentLength is long length && length > MaxArchiveBytes)
                throw new InvalidDataException("Update archive exceeds the 400 MB safety limit.");

            await using var input = await response.Content.ReadAsStreamAsync(ct);
            await using var output = File.Create(archivePath);
            var buffer = new byte[1024 * 1024];
            long bytes = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, ct)) > 0)
            {
                bytes += read;
                if (bytes > MaxArchiveBytes)
                    throw new InvalidDataException("Update archive exceeds the 400 MB safety limit.");
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
            }
        }

        System.IO.Compression.ZipFile.ExtractToDirectory(archivePath, stagingPath);
        if (!File.Exists(Path.Combine(stagingPath, "SnailCommander.exe"))
            || !File.Exists(Path.Combine(stagingPath, "SnailCommander.Updater.exe")))
            throw new InvalidDataException("The downloaded build is missing expected Snail Commander executables.");

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
