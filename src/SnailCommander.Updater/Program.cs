using System.Diagnostics;

namespace SnailCommander.Updater;

/// <summary>
/// Runs independently from a temporary directory, so SnailCommander.exe can be replaced
/// after it exits. Keeps a timestamped rollback backup.
/// </summary>
internal static class Program
{
    private static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SnailCommander");

    private static int Main(string[] args)
    {
        string? staging = null;
        string? destination = null;
        int pid = 0;
        try
        {
            for (int i = 0; i + 1 < args.Length; i += 2)
            {
                switch (args[i])
                {
                    case "--wait-pid": pid = int.Parse(args[i + 1]); break;
                    case "--staging": staging = args[i + 1]; break;
                    case "--destination": destination = args[i + 1]; break;
                    default: throw new ArgumentException("Unexpected argument.");
                }
            }

            if (pid <= 0 || staging is null || destination is null)
                throw new ArgumentException("Missing updater arguments.");

            staging = Path.GetFullPath(staging);
            destination = Path.GetFullPath(destination);
            var exe = Path.Combine(destination, "SnailCommander.exe");
            if (!File.Exists(exe) || !File.Exists(Path.Combine(staging, "SnailCommander.exe")))
                throw new FileNotFoundException("Updater could not validate the old and new app.");

            Log("Update requested. Waiting for Snail Commander to exit.");
            try
            {
                using var process = Process.GetProcessById(pid);
                if (!process.WaitForExit(90000))
                    throw new TimeoutException("The app did not close within 90 seconds; no files were changed.");
            }
            catch (ArgumentException) { /* The process already exited. */ }

            var backup = Path.Combine(DataDir, "Backups",
                DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
            Directory.CreateDirectory(backup);

            var installed = new List<(string Destination, string? OriginalBackup)>();
            try
            {
                foreach (var file in Directory.GetFiles(staging, "*", SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(staging, file);
                    if (relative.StartsWith("..") || Path.IsPathRooted(relative))
                        throw new InvalidDataException("Unsafe update file path.");
                    var target = Path.Combine(destination, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    string? backedUp = null;
                    if (File.Exists(target))
                    {
                        backedUp = Path.Combine(backup, relative);
                        Directory.CreateDirectory(Path.GetDirectoryName(backedUp)!);
                        File.Copy(target, backedUp, overwrite: true);
                    }

                    installed.Add((target, backedUp));
                    File.Copy(file, target, overwrite: true);
                }
                Log("Updated " + installed.Count + " files; backup: " + backup);
            }
            catch
            {
                Log("Installation failed. Restoring replaced files.");
                foreach (var item in installed.AsEnumerable().Reverse())
                {
                    try
                    {
                        if (item.OriginalBackup is not null) File.Copy(item.OriginalBackup, item.Destination, true);
                        else if (File.Exists(item.Destination)) File.Delete(item.Destination);
                    }
                    catch (Exception restoreError) { Log("Rollback error: " + restoreError.Message); }
                }
                throw;
            }

            StartApp(exe);
            Log("Reopened Snail Commander.");
            return 0;
        }
        catch (Exception ex)
        {
            Log("Update ERROR: " + ex);
            try
            {
                if (!string.IsNullOrWhiteSpace(destination))
                {
                    var exe = Path.Combine(destination, "SnailCommander.exe");
                    if (File.Exists(exe)) StartApp(exe);
                }
            }
            catch (Exception restart) { Log("Restart ERROR: " + restart.Message); }
            return 1;
        }
    }

    private static void StartApp(string exe)
    {
        Process.Start(new ProcessStartInfo(exe)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(exe)!
        });
    }

    private static void Log(string line)
    {
        try
        {
            var path = Path.Combine(DataDir, "Logs", "updater.log");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, $"[{DateTime.Now:O}] {line}{Environment.NewLine}");
        }
        catch { /* Logging failure must not block recovery. */ }
    }
}
