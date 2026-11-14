using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using SnailCommander;
using SnailCommander.Core.Automation;
using SnailCommander.Core.Observation;
using SnailCommander.Observation;

internal static class RecoveryIntegrationTests
{
    public static async Task<int> RunAsync(string output)
    {
        Directory.CreateDirectory(output);
        int assertions = 0;
        void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
        using var image = new Bitmap(540, 960);
        using (var graphics = Graphics.FromImage(image))
        using (var title = new Font("Segoe UI", 22))
        using (var message = new Font("Segoe UI", 18))
        {
            graphics.Clear(Color.FromArgb(100, 100, 100));
            graphics.FillRectangle(Brushes.AntiqueWhite, 65, 300, 410, 400);
            graphics.DrawString("Reminder", title, Brushes.Black, 190, 325);
            graphics.DrawString("Login verification failed", message, Brushes.Black, 130, 460);
            graphics.FillRectangle(Brushes.SandyBrown, 200, 600, 140, 50);
            graphics.DrawString("Retry", title, Brushes.Black, 230, 603);
        }
        using var png = new MemoryStream(); image.Save(png, ImageFormat.Png);
        await File.WriteAllBytesAsync(Path.Combine(output, "synthetic-recovery.png"), png.ToArray());
        var observer = new ObservationService(Path.Combine(output, "observations"));
        var frame = await observer.ObserveAsync(png.ToArray(), "synthetic-recovery-fixture", true, origin: ObservationOrigin.Synthetic)
            ?? throw new Exception("No OCR result");
        var focused = await AutomationButtonReader.ReadAsync(png.ToArray(), frame, CancellationToken.None);
        Check(AutomationPolicy.IsLoginFailure(focused), "Native OCR did not recognize the rendered error.");
        Check(focused.Elements.Any(e => e.Text.Equals("Retry", StringComparison.OrdinalIgnoreCase)), "Native OCR did not read Retry.");
        Check(AutomationPolicy.Plan(focused, AutomationPolicy.GamePackage, frame.Device, DateTimeOffset.UtcNow) is null, "Synthetic fixture must never be eligible to act.");
        // Explicit pure-policy simulation only. The clone is neither persisted as live nor sent to an input adapter.
        var simulated = focused with { Origin = ObservationOrigin.LiveAdb };
        Check(AutomationPolicy.Plan(simulated, AutomationPolicy.GamePackage, frame.Device, DateTimeOffset.UtcNow)?.Action == SafeAction.RetryLogin,
            "Rendered modal did not satisfy the strict policy in simulation.");
        string folder = Path.Combine(output, "ledger-" + Guid.NewGuid().ToString("N"));
        var now = DateTimeOffset.UtcNow;
        using (var store = new AutomationPersistence(folder, "synthetic-device"))
        {
            Check(store.Budget.Reserve(now) && store.Budget.Reserve(now) && store.Budget.Reserve(now), "Reserve failed");
            store.SaveBudget();
            bool denied = false;
            try { using var second = new AutomationPersistence(folder, "synthetic-device"); }
            catch (IOException) { denied = true; }
            Check(denied, "Second writer was not excluded.");
            store.WriteRun(new("cancelled", "Synthetic interrupted run", 0, 0, 2));
        }
        using (var restored = new AutomationPersistence(folder, "synthetic-device"))
            Check(!restored.Budget.Reserve(now.AddMinutes(1)), "Reopen reset the recovery budget.");
        string ledger = Directory.GetFiles(folder, "recovery-*.json").Single();
        await File.WriteAllTextAsync(ledger, "{invalid");
        bool rejected = false;
        try { using var invalid = new AutomationPersistence(folder, "synthetic-device"); }
        catch (JsonException) { rejected = true; }
        Check(rejected, "Invalid ledger was reset instead of failing closed.");
        Console.WriteLine($"{assertions} Windows recovery OCR/persistence assertions passed. Synthetic fixture; no game input sent.");
        return 0;
    }
}
