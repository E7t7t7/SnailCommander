using System.Diagnostics;
using SnailCommander.Core.Updates;

namespace SnailCommander.Updater;

internal static class Program
{
    private static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SnailCommander");

    private static int Main(string[] args)
    {
        string? destination = null;
        UpdateTransaction? transaction = null;
        Process? replacement = null;
        Mutex? installationLock = null;
        bool ownsLock = false;
        try
        {
            if (args.Length % 2 != 0) throw new ArgumentException("Updater arguments must be pairs.");
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < args.Length; i += 2)
            {
                if (args[i] is not ("--wait-pid" or "--staging" or "--destination" or "--retain")) throw new ArgumentException("Unexpected updater argument.");
                values.Add(args[i], args[i + 1]);
            }
            if (!values.TryGetValue("--wait-pid", out var pidText) || !int.TryParse(pidText, out var pid) || pid <= 0
                || !values.TryGetValue("--staging", out var staging) || !values.TryGetValue("--destination", out destination))
                throw new ArgumentException("Missing updater arguments.");
            int retention = values.TryGetValue("--retain", out var retain) ? Math.Clamp(int.Parse(retain), 1, 10) : 3;
            staging = Path.GetFullPath(staging);
            destination = Path.GetFullPath(destination);
            var exe = Path.Combine(destination, "SnailCommander.exe");
            if (!File.Exists(exe) || !File.Exists(Path.Combine(staging, "SnailCommander.exe"))
                || !File.Exists(Path.Combine(staging, "SnailCommander.Updater.exe")))
                throw new FileNotFoundException("Updater could not validate the old and new application package.");
            if (staging.StartsWith(destination.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || destination.StartsWith(staging.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || string.Equals(staging, destination, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Staging and installation must be separate directories.");

            installationLock = new Mutex(false, "Local\\SnailCommander.Update");
            try { ownsLock = installationLock.WaitOne(TimeSpan.Zero); }
            catch (AbandonedMutexException) { ownsLock = true; }
            if (!ownsLock) throw new InvalidOperationException("Another update is already running.");

            Log("Waiting for Snail Commander to exit.");
            try
            {
                using var previous = Process.GetProcessById(pid);
                if (!previous.WaitForExit(90000)) throw new TimeoutException("The app did not close within 90 seconds; no files were changed.");
            }
            catch (ArgumentException) { }

            var backups = Path.Combine(DataDir, "Backups");
            Directory.CreateDirectory(backups);
            foreach (var backup in Directory.GetDirectories(backups).OrderByDescending(Path.GetFileName, StringComparer.Ordinal))
            {
                if (!File.Exists(Path.Combine(backup, "journal.json"))) continue;
                var previous = UpdateTransaction.Load(backup);
                var journal = System.Text.Json.JsonSerializer.Deserialize<UpdateJournal>(File.ReadAllText(Path.Combine(backup, "journal.json")))!;
                if (string.Equals(journal.Destination, destination, StringComparison.OrdinalIgnoreCase)
                    && previous.Status is "installing" or "awaiting-health")
                {
                    Log("Recovering interrupted update: " + backup);
                    previous.Rollback();
                }
            }
            var backupPath = Path.Combine(backups, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]);
            transaction = UpdateTransaction.Create(destination, backupPath,
                FileVersionInfo.GetVersionInfo(exe).ProductVersion,
                FileVersionInfo.GetVersionInfo(Path.Combine(staging, "SnailCommander.exe")).ProductVersion);
            transaction.Install(staging);
            Log("Installed package; checking startup health. Backup: " + backupPath);

            var token = Guid.NewGuid().ToString("N");
            var healthRoot = Path.Combine(Path.GetTempPath(), "SnailCommander");
            Directory.CreateDirectory(healthRoot);
            var healthPath = Path.Combine(healthRoot, "health-" + token + ".ok");
            var start = NewStartInfo(exe);
            start.ArgumentList.Add("--update-health"); start.ArgumentList.Add(healthPath);
            start.ArgumentList.Add("--update-token"); start.ArgumentList.Add(token);
            replacement = Process.Start(start) ?? throw new IOException("Updated application could not start.");
            var deadline = Stopwatch.StartNew();
            var acknowledged = false;
            while (deadline.Elapsed < TimeSpan.FromSeconds(45))
            {
                if (replacement.HasExited) throw new IOException("Updated application exited before startup health passed.");
                try { acknowledged = File.Exists(healthPath) && File.ReadAllText(healthPath) == token; }
                catch (IOException) { }
                if (acknowledged) break;
                Thread.Sleep(250);
            }
            if (!acknowledged) throw new TimeoutException("Updated application did not acknowledge startup within 45 seconds.");
            if (replacement.WaitForExit(3000)) throw new IOException("Updated application exited immediately after startup.");
            transaction.Commit();
            try { File.Delete(healthPath); } catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { Log("Health cleanup: " + cleanup.Message); }
            try { UpdateTransaction.PruneHealthyBackups(backups, retention); }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException or InvalidDataException) { Log("Backup retention: " + cleanup.Message); }
            Log("Startup verified; update complete.");
            return 0;
        }
        catch (Exception ex)
        {
            Log("Update ERROR: " + ex);
            try { if (transaction?.Status != "healthy" && replacement is not null && !replacement.HasExited) { replacement.Kill(true); replacement.WaitForExit(10000); } }
            catch (Exception stop) { Log("Could not stop failed replacement: " + stop.Message); }
            if (transaction is not null && transaction.Status != "healthy")
            {
                try { transaction.Rollback(); Log("Restored previous installation."); }
                catch (Exception restore) { Log("ROLLBACK ERROR: " + restore); return 2; }
                try { _ = Process.Start(NewStartInfo(Path.Combine(destination!, "SnailCommander.exe"))); }
                catch (Exception restart) { Log("Restart ERROR: " + restart.Message); }
            }
            return 1;
        }
        finally
        {
            replacement?.Dispose();
            if (ownsLock) installationLock?.ReleaseMutex();
            installationLock?.Dispose();
        }
    }

    private static ProcessStartInfo NewStartInfo(string exe) => new(exe)
    {
        UseShellExecute = false,
        WorkingDirectory = Path.GetDirectoryName(exe)!,
        CreateNoWindow = true
    };

    private static void Log(string line)
    {
        try
        {
            var path = Path.Combine(DataDir, "Logs", "updater.log");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, $"[{DateTime.Now:O}] {line}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
