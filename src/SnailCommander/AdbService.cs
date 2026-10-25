using System.Diagnostics;

namespace SnailCommander;

/// <summary>
/// Read-only ADB diagnostics and screen capture; no tapping, swiping or game interaction.
/// </summary>
internal static class AdbService
{
    public static string? DiscoverAdb()
    {
        var candidates = new List<string>();
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        foreach (var root in new[] { programFiles, programFilesX86 })
        {
            candidates.Add(Path.Combine(root, "BlueStacks_nxt", "HD-Adb.exe"));
            candidates.Add(Path.Combine(root, "BlueStacks", "HD-Adb.exe"));
            candidates.Add(Path.Combine(root, "Android", "platform-tools", "adb.exe"));
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        candidates.Add(Path.Combine(localAppData, "Android", "Sdk", "platform-tools", "adb.exe"));

        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (!string.IsNullOrWhiteSpace(dir))
            {
                candidates.Add(Path.Combine(dir.Trim('"'), "adb.exe"));
                candidates.Add(Path.Combine(dir.Trim('"'), "HD-Adb.exe"));
            }
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    public static async Task<string> ConnectAsync(string adb, string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentException("ADB endpoint is required.");

        return await RunTextAsync(adb, "connect", endpoint.Trim());
    }

    public static async Task<IReadOnlyList<string>> GetDevicesAsync(string adb)
    {
        var output = await RunTextAsync(adb, "devices");
        var devices = new List<string>();

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var columns = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (columns.Length >= 2 && columns[1] == "device")
                devices.Add(columns[0]);
        }

        return devices;
    }

    public static async Task<byte[]> CaptureAsync(string adb, string serial)
    {
        if (string.IsNullOrWhiteSpace(serial))
            throw new ArgumentException("Choose a connected ADB device first.");

        using var process = Start(adb, "-s", serial, "exec-out", "screencap", "-p");
        using var output = new MemoryStream();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));

        var errorTask = process.StandardError.ReadToEndAsync();
        try
        {
            await Task.WhenAll(
                process.StandardOutput.BaseStream.CopyToAsync(output, timeout.Token),
                process.WaitForExitAsync(timeout.Token));
        }
        catch (OperationCanceledException)
        {
            Terminate(process);
            throw new TimeoutException("ADB screenshot timed out after 25 seconds.");
        }

        var error = await errorTask;
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"ADB screenshot failed: {error.Trim()}");

        var bytes = output.ToArray();
        byte[] png = { 137, 80, 78, 71, 13, 10, 26, 10 };
        if (bytes.Length < png.Length || !bytes.AsSpan(0, png.Length).SequenceEqual(png))
            throw new InvalidDataException("Capture did not return a valid PNG. Check the selected device and BlueStacks ADB support.");

        return bytes;
    }

    private static async Task<string> RunTextAsync(string adb, params string[] arguments)
    {
        using var process = Start(adb, arguments);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            Terminate(process);
            throw new TimeoutException("ADB command timed out after 20 seconds.");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"ADB exit code {process.ExitCode}: {stderr.Trim()} {stdout.Trim()}");

        return stdout.Trim();
    }

    private static Process Start(string adb, params string[] arguments)
    {
        if (!File.Exists(adb))
            throw new FileNotFoundException("ADB executable not found. Use Browse to select HD-Adb.exe or adb.exe.", adb);

        var info = new ProcessStartInfo(adb)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var arg in arguments)
            info.ArgumentList.Add(arg);

        return Process.Start(info) ?? throw new InvalidOperationException("Failed to launch ADB.");
    }

    private static void Terminate(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
    }
}
