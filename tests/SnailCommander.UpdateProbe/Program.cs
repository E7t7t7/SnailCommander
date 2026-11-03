using SnailCommander.Core.Updates;

// Disposable process fixture. Never shipped with the application or used for gameplay.
string directory = AppContext.BaseDirectory;
string mode = File.ReadAllText(Path.Combine(directory, "probe-mode.txt")).Trim();
File.WriteAllText(Path.Combine(directory, $"probe-started-{Environment.ProcessId}.txt"), mode);
if (mode == "exit") return 17;
if (mode != "noack") StartupHealth.Acknowledge(args);
if (mode == "early-exit")
{
    int index = Array.IndexOf(args, "--data-root");
    string log = Path.Combine(args[index + 1], "Logs", "updater.log");
    var deadline = System.Diagnostics.Stopwatch.StartNew();
    while (deadline.Elapsed < TimeSpan.FromSeconds(10))
    {
        try { if (File.ReadAllText(log).Contains("Startup acknowledged; verifying grace period.")) return 18; }
        catch (IOException) { }
        Thread.Sleep(20);
    }
    return 19;
}
Thread.Sleep(TimeSpan.FromMinutes(2));
return 0;
