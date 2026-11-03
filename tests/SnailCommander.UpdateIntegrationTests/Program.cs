using System.Diagnostics;
using System.Text.Json;
using SnailCommander.Core.Updates;

string Required(string key)
{
    int index = Array.IndexOf(args, key);
    if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Missing " + key);
    return Path.GetFullPath(args[index + 1]);
}
string worker = Required("--worker");
string probeDirectory = Required("--probe");
string output = Path.TrimEndingDirectorySeparator(Required("--output"));
int guiIndex = Array.IndexOf(args, "--gui");
string? guiDirectory = guiIndex >= 0 && guiIndex + 1 < args.Length ? Path.GetFullPath(args[guiIndex + 1]) : null;
if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Real Windows updater processes are required.");
if (!File.Exists(worker) || !File.Exists(Path.Combine(probeDirectory, "SnailCommander.exe")))
    throw new FileNotFoundException("Publish the updater and process probe before running this suite.");
Directory.CreateDirectory(output);
string runRoot = Path.Combine(output, "update-e2e-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(runRoot);
File.WriteAllText(Path.Combine(runRoot, ".owned-test-directory"), runRoot);
int assertions = 0;
var reports = new List<object>();

void Assert(bool value, string message)
{
    assertions++;
    if (!value) throw new Exception(message);
}

async Task Until(Func<bool> condition, string message, int seconds = 15)
{
    var watch = Stopwatch.StartNew();
    while (watch.Elapsed < TimeSpan.FromSeconds(seconds))
    {
        try { if (condition()) return; }
        catch (IOException) { }
        await Task.Delay(100);
    }
    throw new TimeoutException(message);
}

Process Start(string executable, params string[] arguments)
{
    var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true,
        WorkingDirectory = Path.GetDirectoryName(executable)!, WindowStyle = ProcessWindowStyle.Hidden };
    foreach (string argument in arguments) info.ArgumentList.Add(argument);
    return Process.Start(info) ?? throw new IOException("Cannot start " + executable);
}

void CopyPackage(string source, string target)
{
    Directory.CreateDirectory(target);
    foreach (var path in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
    {
        string relative = Path.GetRelativePath(source, path);
        string destination = Path.Combine(target, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(path, destination);
    }
}

void StopOwnedProcess(Process process, string expectedExecutable)
{
    if (process.HasExited) return;
    string? actual = process.MainModule?.FileName;
    if (!string.Equals(Path.GetFullPath(actual ?? ""), Path.GetFullPath(expectedExecutable), StringComparison.OrdinalIgnoreCase))
        throw new Exception("Refusing to stop a process outside this test installation.");
    process.Kill(entireProcessTree: true);
    if (!process.WaitForExit(10000)) throw new TimeoutException("Test process did not exit.");
}

void StopProbeChildren(string installed)
{
    foreach (string marker in Directory.GetFiles(installed, "probe-started-*.txt"))
    {
        string name = Path.GetFileNameWithoutExtension(marker);
        if (!int.TryParse(name["probe-started-".Length..], out int pid)) continue;
        try { using var child = Process.GetProcessById(pid); StopOwnedProcess(child, Path.Combine(installed, "SnailCommander.exe")); }
        catch (ArgumentException) { }
    }
}

async Task RunScenario(string name, string mode, bool copyFailure = false, bool gui = false, bool backupFailure = false)
{
    var watch = Stopwatch.StartNew();
    string root = Path.Combine(runRoot, name);
    string installed = Path.Combine(root, "installed");
    string staging = Path.Combine(root, "staging");
    string state = Path.Combine(root, "profile");
    string app = Path.Combine(installed, "SnailCommander.exe");
    CopyPackage(probeDirectory, installed);
    CopyPackage(gui ? guiDirectory! : probeDirectory, staging);
    File.WriteAllText(Path.Combine(installed, "probe-mode.txt"), "old");
    if (!gui) File.WriteAllText(Path.Combine(staging, "probe-mode.txt"), mode);
    File.Copy(worker, Path.Combine(staging, "SnailCommander.Updater.exe"), true);
    File.WriteAllText(Path.Combine(installed, "unrelated-settings.txt"), "preserve-local-file");
    File.WriteAllText(Path.Combine(staging, "new-package-file.txt"), "new-content");
    Directory.CreateDirectory(state);
    // The real GUI uses only this disposable profile, with preview disabled.
    string settings = "{\"LivePreviewEnabled\":false,\"AutoDownloadUpdates\":false,\"PreferredDevice\":\"\"}";
    File.WriteAllText(Path.Combine(state, "settings.json"), settings);
    File.WriteAllText(Path.Combine(state, "minions.json"), "{\"AccountName\":\"E2E fixture\",\"Leadership\":0,\"Minions\":[],\"Bonuses\":[]}");
    if (backupFailure) File.WriteAllText(Path.Combine(state, "Backups"), "block backup directory creation");
    if (copyFailure)
    {
        Directory.CreateDirectory(Path.Combine(installed, "zz-file-conflict"));
        File.WriteAllText(Path.Combine(staging, "zz-file-conflict"), "cannot overwrite existing directory");
    }
    bool success = mode == "healthy" && !copyFailure && !backupFailure;
    using var old = Start(app);
    Process? updater = null;
    Process? guiProcess = null;
    try
    {
        await Until(() => File.Exists(Path.Combine(installed, $"probe-started-{old.Id}.txt")), "Old fixture did not start.");
        updater = Start(worker, "--wait-pid", old.Id.ToString(), "--staging", staging,
            "--destination", installed, "--data-root", state, "--health-timeout", "10", "--health-grace", "1");
        string log = Path.Combine(state, "Logs", "updater.log");
        await Until(() => File.Exists(log) && File.ReadAllText(log).Contains("Waiting for Snail Commander"), "Worker did not wait for old app.");
        Assert(!Directory.Exists(Path.Combine(state, "Backups")), name + ": no replacement starts while old process is running");
        Assert(File.ReadAllText(Path.Combine(installed, "probe-mode.txt")) == "old", name + ": old package untouched while running");
        StopOwnedProcess(old, app);
        await updater.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(40));
        Assert(updater.ExitCode == (success ? 0 : 1), name + ": unexpected updater exit " + updater.ExitCode + "\n" + File.ReadAllText(log));
        if (mode == "early-exit") Assert(File.ReadAllText(log).Contains("exited immediately after startup"), name + ": grace-period failure was not exercised");
        if (mode == "noack") Assert(File.ReadAllText(log).Contains("did not acknowledge startup"), name + ": missing acknowledgment deadline was not exercised");
        var transaction = backupFailure ? null : UpdateTransaction.Load(Directory.GetDirectories(Path.Combine(state, "Backups")).Single());
        Assert(backupFailure ? !Directory.Exists(Path.Combine(state, "Backups"))
            : transaction!.Status == (success ? "healthy" : "rolled-back"), name + ": wrong persisted transaction state");
        Assert(File.ReadAllText(Path.Combine(installed, "unrelated-settings.txt")) == "preserve-local-file", name + ": unrelated installation data changed");
        Assert(File.ReadAllText(Path.Combine(state, "minions.json")).Contains("E2E fixture"), name + ": profile account data changed");
        Assert(File.Exists(Path.Combine(installed, "new-package-file.txt")) == success, name + ": new files not installed/restored correctly");
        if (gui)
        {
            // PID is logged by the real worker and tied to this installation below.
            string line = File.ReadAllLines(log).Single(l => l.Contains("Replacement PID: "));
            int pid = int.Parse(line[(line.IndexOf("Replacement PID: ", StringComparison.Ordinal) + "Replacement PID: ".Length)..]);
            guiProcess = Process.GetProcessById(pid);
            Assert(!guiProcess.HasExited && guiProcess.Responding, "Real GUI did not remain responsive after health acknowledgment");
            Assert(File.Exists(Path.Combine(state, "Logs", "session.log")), "Real GUI did not use isolated log directory");
            Assert(Directory.Exists(Path.Combine(state, "Captures")), "Real GUI did not use isolated capture directory");
        }
        else if (success)
            Assert(File.ReadAllText(Path.Combine(installed, "probe-mode.txt")) == mode, name + ": new fixture was not installed");
        else
        {
            Assert(File.ReadAllText(Path.Combine(installed, "probe-mode.txt")) == "old", name + ": original package was not restored");
            Assert(!File.Exists(Path.Combine(installed, "SnailCommander.Updater.exe")), name + ": newly introduced updater survived rollback");
            await Until(() => Directory.GetFiles(installed, "probe-started-*.txt").Any(p =>
                Path.GetFileName(p) != $"probe-started-{old.Id}.txt" && File.ReadAllText(p) == "old"), "Restored previous app did not restart.");
            Assert(true, name + ": restored app restarted");
        }
        if (!gui) Assert(File.ReadAllText(Path.Combine(state, "settings.json")) == settings, name + ": worker changed profile preferences");
        reports.Add(new { scenario = name, status = "passed", elapsedMilliseconds = watch.ElapsedMilliseconds, updaterExit = updater.ExitCode });
        Console.WriteLine($"PASS {name}: updater {updater.ExitCode}, {transaction?.Status ?? "unchanged"}, {watch.ElapsedMilliseconds} ms");
    }
    finally
    {
        if (updater is not null) { StopOwnedProcess(updater, worker); updater.Dispose(); }
        if (guiProcess is not null) { StopOwnedProcess(guiProcess, app); guiProcess.Dispose(); }
        string workerLog = Path.Combine(state, "Logs", "updater.log");
        if (File.Exists(workerLog))
            foreach (string line in File.ReadAllLines(workerLog).Where(l => l.Contains("Replacement PID: ")))
            {
                if (!int.TryParse(line[(line.IndexOf("Replacement PID: ", StringComparison.Ordinal) + "Replacement PID: ".Length)..], out int pid)) continue;
                try { using var child = Process.GetProcessById(pid); StopOwnedProcess(child, app); }
                catch (ArgumentException) { }
            }
        StopOwnedProcess(old, app);
        StopProbeChildren(installed);
    }
}

async Task RunInterruptedRecovery()
{
    var watch = Stopwatch.StartNew();
    string root = Path.Combine(runRoot, "interrupted-worker-recovery");
    string installed = Path.Combine(root, "installed");
    string staging = Path.Combine(root, "staging");
    string state = Path.Combine(root, "profile");
    string app = Path.Combine(installed, "SnailCommander.exe");
    CopyPackage(probeDirectory, installed);
    CopyPackage(probeDirectory, staging);
    File.WriteAllText(Path.Combine(installed, "probe-mode.txt"), "old");
    File.WriteAllText(Path.Combine(staging, "probe-mode.txt"), "noack");
    File.Copy(worker, Path.Combine(staging, "SnailCommander.Updater.exe"));
    File.WriteAllText(Path.Combine(installed, "unrelated.txt"), "preserve");
    using var old = Start(app);
    Process? first = null;
    Process? replacement = null;
    Process? second = null;
    try
    {
        await Until(() => File.Exists(Path.Combine(installed, $"probe-started-{old.Id}.txt")), "Recovery fixture did not start.");
        first = Start(worker, "--wait-pid", old.Id.ToString(), "--staging", staging, "--destination", installed,
            "--data-root", state, "--health-timeout", "120", "--health-grace", "1");
        string log = Path.Combine(state, "Logs", "updater.log");
        await Until(() => File.Exists(log) && File.ReadAllText(log).Contains("Waiting for Snail Commander"), "First recovery worker did not start.");
        StopOwnedProcess(old, app);
        await Until(() => File.ReadAllText(log).Contains("Replacement PID: "), "First recovery worker did not launch a replacement.");
        string pidLine = File.ReadAllLines(log).Single(l => l.Contains("Replacement PID: "));
        int pid = int.Parse(pidLine[(pidLine.IndexOf("Replacement PID: ", StringComparison.Ordinal) + "Replacement PID: ".Length)..]);
        replacement = Process.GetProcessById(pid);
        await Until(() => File.Exists(Path.Combine(installed, $"probe-started-{pid}.txt")), "Pending replacement did not start.");
        // Simulate the worker being killed while its installed app remains running.
        Assert(string.Equals(first.MainModule?.FileName, worker, StringComparison.OrdinalIgnoreCase), "Crash fixture owns worker executable");
        first.Kill(); first.WaitForExit();
        string pending = Directory.GetDirectories(Path.Combine(state, "Backups")).Single();
        Assert(UpdateTransaction.Load(pending).Status == "awaiting-health", "Killed worker preserves an unfinished recovery checkpoint");
        Assert(!replacement.HasExited, "Killing only the worker leaves its replacement alive");
        File.WriteAllText(Path.Combine(staging, "probe-mode.txt"), "healthy");
        second = Start(worker, "--wait-pid", pid.ToString(), "--staging", staging, "--destination", installed,
            "--data-root", state, "--health-timeout", "10", "--health-grace", "1");
        StopOwnedProcess(replacement, app); // The next requesting application exits, as in the real handoff.
        await second.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(35));
        Assert(second.ExitCode == 0, "Next attempt recovers interrupted installation and succeeds: " + File.ReadAllText(log));
        var statuses = Directory.GetDirectories(Path.Combine(state, "Backups")).Select(p => UpdateTransaction.Load(p).Status).ToArray();
        Assert(statuses.Length == 2 && statuses.Contains("rolled-back") && statuses.Contains("healthy"), "Interrupted and successful journals retain separate outcomes");
        Assert(File.ReadAllText(log).Contains("Recovering interrupted update"), "Recovery used the persisted interrupted journal");
        Assert(File.ReadAllText(Path.Combine(installed, "probe-mode.txt")) == "healthy", "Recovered installation contains the successful replacement");
        Assert(File.ReadAllText(Path.Combine(installed, "unrelated.txt")) == "preserve", "Interrupted recovery preserves unrelated local files");
        reports.Add(new { scenario = "interrupted-worker-recovery", status = "passed", elapsedMilliseconds = watch.ElapsedMilliseconds, updaterExit = second.ExitCode });
        Console.WriteLine($"PASS interrupted-worker-recovery: recovered after worker termination, {watch.ElapsedMilliseconds} ms");
    }
    finally
    {
        if (first is not null) { StopOwnedProcess(first, worker); first.Dispose(); }
        if (second is not null) { StopOwnedProcess(second, worker); second.Dispose(); }
        if (replacement is not null) { StopOwnedProcess(replacement, app); replacement.Dispose(); }
        StopOwnedProcess(old, app);
        StopProbeChildren(installed);
    }
}

try
{
    await RunScenario("healthy-install", "healthy");
    await RunScenario("failed-startup", "exit");
    await RunScenario("missing-acknowledgment", "noack");
    await RunScenario("exit-after-acknowledgment", "early-exit");
    await RunScenario("mid-install-failure", "healthy", copyFailure: true);
    await RunScenario("backup-creation-failure", "healthy", backupFailure: true);
    await RunInterruptedRecovery();
    if (guiDirectory is not null) await RunScenario("real-gui-install", "healthy", gui: true);
    await File.WriteAllTextAsync(Path.Combine(output, "report.json"), JsonSerializer.Serialize(new { assertions, failures = 0, reports }, new JsonSerializerOptions { WriteIndented = true }));
    // Only remove the uniquely named directory this runner created, after verifying its absolute parent and owner marker.
    string resolved = Path.GetFullPath(runRoot);
    if (Directory.GetParent(resolved)?.FullName != Path.GetFullPath(output)
        || !Path.GetFileName(resolved).StartsWith("update-e2e-", StringComparison.Ordinal)
        || File.ReadAllText(Path.Combine(resolved, ".owned-test-directory")) != runRoot)
        throw new InvalidOperationException("Unsafe test cleanup path.");
    Directory.Delete(resolved, recursive: true);
    Console.WriteLine($"{assertions} process assertions passed. Report: {Path.Combine(output, "report.json")}");
    return 0;
}
catch (Exception error)
{
    await File.WriteAllTextAsync(Path.Combine(output, "report.json"), JsonSerializer.Serialize(new { assertions, failures = 1, reports, error = error.ToString(), evidence = runRoot }, new JsonSerializerOptions { WriteIndented = true }));
    Console.Error.WriteLine(error);
    Console.Error.WriteLine("Evidence preserved: " + runRoot);
    return 1;
}
