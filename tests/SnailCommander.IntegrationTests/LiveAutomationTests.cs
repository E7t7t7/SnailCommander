using System.Text.Json;
using SnailCommander;
using SnailCommander.Core.Automation;
using SnailCommander.Core.Observation;
using SnailCommander.Observation;

internal static class LiveAutomationTests
{
    public static async Task<int> RunAsync(string adb, string device, string output)
    {
        Directory.CreateDirectory(output);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        if (!await AdbService.WaitForAndroidBootAsync(adb, device, deadline.Token)) throw new TimeoutException("Android boot deadline expired.");
        if (await AdbService.GetForegroundPackageAsync(adb, device, deadline.Token) != AutomationPolicy.GamePackage)
            await AdbService.RunCancelableAsync(adb, deadline.Token, "-s", device, "shell", "monkey", "-p", AutomationPolicy.GamePackage, "-c", "android.intent.category.LAUNCHER", "1");
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        using var gate = new SemaphoreSlim(1, 1);
        using var persistence = new AutomationPersistence(Path.Combine(output, "Automation"), device);
        var observer = new ObservationService(Path.Combine(output, "observations"), retryUnknownAtLargerScale: true);
        int homeFrames = 0;
        async Task<ScreenObservation> Read(CancellationToken ct)
        {
            await gate.WaitAsync(ct);
            byte[] png;
            try { png = await AdbService.CaptureAsync(adb, device, ct); }
            finally { gate.Release(); }
            var frame = await observer.ObserveAsync(png, device, true, ct, ObservationOrigin.LiveAdb) ?? throw new Exception("No observation.");
            frame = await AutomationButtonReader.ReadAsync(png, frame, ct);
            if (AutomationPolicy.IsLiveHome(frame, AutomationPolicy.GamePackage, device, DateTimeOffset.UtcNow)) homeFrames++;
            return frame;
        }
        var port = new AdbAutomationPort(adb, device, gate, Read, ct => { ct.ThrowIfCancellationRequested(); return Task.CompletedTask; },
            () => device, () => 0, () => false, Console.WriteLine,
            (stage, frame, action) => { if (stage == "intent") persistence.SaveBudget(); Console.WriteLine($"Action checkpoint: {stage} {action.Action}"); });
        var result = await new AutomationRunner(port, device, persistence.Budget).RunAsync(deadline.Token);
        persistence.WriteRun(result);
        Console.WriteLine(JsonSerializer.Serialize(new { result, homeFrames }));
        return result.Outcome == "needs-attention" ? 2 : 0;
    }
}
