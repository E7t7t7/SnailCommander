using System.Text.Json;
using SnailCommander.Core.Automation;
using SnailCommander.Core.Observation;

namespace SnailCommander.Tests;

internal static class AutomationRunnerTests
{
    private const string Device = "127.0.0.1:5555";
    private static ScreenObservation Frame(string kind, DateTimeOffset now, int sequence) => new()
    {
        Device = Device, Origin = ObservationOrigin.LiveAdb, CapturedAt = now, Width = 540, Height = 960,
        Fingerprint = sequence.ToString(), Id = "synthetic-" + sequence,
        Text = kind switch { "retry" => "Reminder Login verification failed Retry", "login" => "Server Version 1 Start", "reward" => "Rewards Gained OK", "home" => "Offering Exploration Cloning Tank Healing Tank", "research" => "Research", _ => "Unknown fixture" },
        Elements = kind switch
        {
            "retry" => new[] { new TextElement("Login verification failed", 140, 460, 230, 25), new TextElement("Retry", 200, 600, 140, 45) },
            "login" => new[] { new TextElement("Start", 200, 700, 140, 45) },
            "reward" => new[] { new TextElement("OK", 200, 600, 140, 45) },
            "home" => new[] { new TextElement("Offering", 20, 120, 100, 20), new TextElement("Exploration", 20, 160, 100, 20), new TextElement("Cloning", 20, 200, 100, 20) },
            _ => Array.Empty<TextElement>()
        },
        Classification = kind == "research" ? new(ScreenKind.Research, .8, new[] { "synthetic" }) : new(ScreenKind.Unknown, 0, Array.Empty<string>())
    };
    public static void Run(Action<bool, string> assert) => RunAsync(assert).GetAwaiter().GetResult();
    private static async Task RunAsync(Action<bool, string> assert)
    {
        var now = DateTimeOffset.UtcNow;
        var retry = Frame("retry", now, 1);
        assert(AutomationPolicy.Plan(retry, AutomationPolicy.GamePackage, Device, now)?.Action == SafeAction.RetryLogin, "central explicit login error and Retry allowed");
        assert(AutomationPolicy.Plan(retry with { Text = "Retry" }, AutomationPolicy.GamePackage, Device, now) is null, "generic retry denied");
        assert(AutomationPolicy.Plan(retry with { Elements = new[] { new TextElement("Login verification failed", 20, 850, 200, 20), new TextElement("Retry", 200, 600, 140, 45) } }, AutomationPolicy.GamePackage, Device, now) is null, "chat error text cannot authorize retry");
        assert(AutomationPolicy.Plan(retry with { Text = retry.Text + " Purchase" }, AutomationPolicy.GamePackage, Device, now) is null, "purchase retry denied");
        assert(AutomationPolicy.Plan(retry with { Elements = new[] { new TextElement("Login verification failed", 140, 460, 230, 25), new TextElement("Retry", 20, 600, 60, 45) } }, AutomationPolicy.GamePackage, Device, now) is null, "off-center retry target denied");
        assert(AutomationPolicy.IsLiveHome(Frame("home", now, 1), AutomationPolicy.GamePackage, Device, now), "spatial task board recognizes home");
        assert(!AutomationPolicy.IsLiveHome(Frame("home", now, 1) with { Origin = ObservationOrigin.Synthetic }, AutomationPolicy.GamePackage, Device, now), "synthetic board excluded");
        assert(!AutomationPolicy.IsLiveHome(Frame("home", now, 1) with { Text = "Rewards Gained" }, AutomationPolicy.GamePackage, Device, now), "reward modal over home excluded");
        assert(!AutomationPolicy.Verified(new(SafeAction.Login, 270, 722), Frame("login", now, 1), retry with { CapturedAt = now.AddSeconds(1) }, AutomationPolicy.GamePackage), "explicit login failure never verifies login");
        var noticeAfterRetry = retry with { Text = "Maintenance Tap anywhere to close notice", CapturedAt = now.AddSeconds(1), Fingerprint = "new-notice", Elements = new[] { new TextElement("Tap", 60, 850, 40, 30) } };
        assert(AutomationPolicy.Verified(new(SafeAction.RetryLogin, 270, 622), retry, noticeAfterRetry, AutomationPolicy.GamePackage), "retry to explicit notice verifies independently of generic screen classification");
        var budget = new RecoveryBudget();
        assert(budget.Reserve(now) && budget.Reserve(now) && budget.Reserve(now) && !budget.Reserve(now), "three attempts maximum");
        var restored = JsonSerializer.Deserialize<RecoveryBudget>(JsonSerializer.Serialize(budget))!;
        assert(!restored.Reserve(now.AddMinutes(14)), "restart does not reset budget");
        assert(restored.Reserve(now.AddMinutes(15)), "expired reservations release after fifteen minutes");

        async Task<(AutomationRunResult Result, Fake Port, RecoveryBudget Budget)> Run(Func<Fake, string> read, Action<Fake>? configure = null, RecoveryBudget? saved = null)
        {
            var port = new Fake(read); configure?.Invoke(port); var ledger = saved ?? new RecoveryBudget();
            return (await new AutomationRunner(port, Device, ledger).RunAsync(port.Cancellation.Token), port, ledger);
        }
        var unknown = await Run(_ => "unknown");
        assert(unknown.Result.Outcome == "needs-attention" && unknown.Port.Inputs.Count == 0 && unknown.Result.Observations == 12, "unknown run stops bounded without input");
        var home = await Run(_ => "home");
        assert(home.Result.Outcome == "complete" && home.Port.Inputs.Count == 0 && home.Port.Delays.All(d => d == TimeSpan.FromSeconds(10)), "home monitoring schedules bounded no-input checks");
        var recovery = await Run(p => p.Inputs.Count == 0 ? "login" : p.Inputs.Count == 1 ? "retry" : "home");
        assert(recovery.Port.Inputs.Select(a => a.Action).SequenceEqual(new[] { SafeAction.Login, SafeAction.RetryLogin }), "login failure followed by one context-checked recovery");
        assert(recovery.Result.VerifiedActions == 1 && recovery.Port.Stages.Contains("login-error-observed"), "failed login not counted as successful action");
        assert(recovery.Budget.Used(recovery.Port.UtcNow) == 1, "recovery reservation recorded");
        var cap = await Run(_ => "retry");
        assert(cap.Port.Inputs.Count == 3 && cap.Result.Reason.Contains("three attempts"), "persistent error capped at three recognized retries");
        assert(cap.Port.Delays.Contains(TimeSpan.FromSeconds(5)) && cap.Port.Delays.Contains(TimeSpan.FromSeconds(15)) && cap.Port.Delays.Contains(TimeSpan.FromSeconds(30)), "recovery backoff 5 15 30 seconds");
        assert(cap.Result.VerifiedActions == 0, "persistent errors never count as verified recovery");
        var exhausted = await Run(_ => "retry", saved: new RecoveryBudget { Attempts = new() { now, now, now } });
        assert(exhausted.Port.Inputs.Count == 0, "persisted exhausted budget prevents input");
        var unresolved = await Run(p => p.Inputs.Count == 0 ? "retry" : "unknown");
        assert(unresolved.Port.Inputs.Count == 1 && unresolved.Result.Outcome == "needs-attention", "unknown recovery outcome never triggers blind retry");
        var reward = await Run(p => p.Inputs.Count == 0 ? "reward" : "research");
        assert(reward.Port.Inputs.Count == 1 && reward.Result.VerifiedActions == 1, "reward verified through a known distinct screen");
        var unchanged = await Run(_ => "reward");
        assert(unchanged.Port.Inputs.Count == 1 && unchanged.Result.VerifiedActions == 0, "unverified reward not repeated");
        var paused = await Run(p => p.Observations <= 2 ? "reward" : "unknown", p => p.ChangeRevisionOnSecondObservation = true);
        assert(paused.Port.Inputs.Count == 0, "pause/resume revision invalidates old preflight");
        var switched = await Run(_ => "reward", p => p.ChangeDeviceOnSecondObservation = true);
        assert(switched.Port.Inputs.Count == 0 && switched.Result.Reason.Contains("device changed"), "device switch blocks pending input");
        var failure = await Run(_ => "retry", p => p.FailIntent = true);
        assert(failure.Port.Inputs.Count == 0 && failure.Result.Outcome == "needs-attention", "persistence failure prevents input");
        var cancelled = await Run(_ => "retry", p => p.CancelOnIntent = true);
        assert(cancelled.Result.Outcome == "cancelled" && cancelled.Port.Inputs.Count == 0 && cancelled.Budget.Attempts.Count == 1, "interrupted intent reserves budget without replay");
        var cooldown = await Run(_ => "retry", p => p.CancelOnDelay = true);
        assert(cooldown.Result.Outcome == "cancelled" && cooldown.Port.Inputs.Count == 0, "Stop during cooldown sends no input");
        var changed = await Run(p => p.Observations == 1 ? "retry" : "home");
        assert(changed.Port.Inputs.Count == 0 && changed.Budget.Attempts.Count == 0, "recovery cooldown reobserves instead of using a stale Retry");
        var interrupted = await Run(_ => "retry", p => p.CancelOnSend = true);
        assert(interrupted.Result.Outcome == "cancelled" && interrupted.Result.Inputs == 1 && interrupted.Port.Stages.Contains("cancelled-unverified"), "uncertain dispatch cancellation is recorded without replay");
        var timeout = await Run(_ => "reward", p => p.TimeJumpOnSend = true);
        assert(timeout.Result.Outcome == "needs-attention" && timeout.Port.Stages.Contains("session-limit-unverified"), "session deadline during verification is not successful completion");
        var churn = await Run(_ => "reward", p => p.RefuseSend = true);
        assert(churn.Result.Outcome == "complete" && churn.Port.Inputs.Count == 0 && churn.Result.Observations <= 100, "preflight churn has a global observation bound");
        var foreign = await Run(_ => "reward", p => p.Package = "foreign.package");
        assert(foreign.Port.Inputs.Count == 0, "foreign foreground cannot execute through runner");
    }

    private sealed class Fake(Func<Fake, string> read) : IAutomationPort
    {
        public CancellationTokenSource Cancellation { get; } = new();
        public DateTimeOffset UtcNow { get; private set; } = DateTimeOffset.UtcNow;
        public string SelectedDevice { get; private set; } = Device;
        public int Revision { get; private set; }
        public int Observations { get; private set; }
        public string Package { get; set; } = AutomationPolicy.GamePackage;
        public bool ChangeRevisionOnSecondObservation, ChangeDeviceOnSecondObservation, FailIntent, CancelOnIntent, CancelOnDelay, RefuseSend, CancelOnSend, TimeJumpOnSend;
        public List<PlannedTap> Inputs { get; } = new();
        public List<string> Stages { get; } = new();
        public List<TimeSpan> Delays { get; } = new();
        public Task WaitReadyAsync(CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.CompletedTask; }
        public Task<AutomationSnapshot> ObserveAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Observations++; UtcNow = UtcNow.AddSeconds(1);
            if (Observations == 2 && ChangeRevisionOnSecondObservation) Revision++;
            if (Observations == 2 && ChangeDeviceOnSecondObservation) SelectedDevice = "other";
            return Task.FromResult(new AutomationSnapshot(Frame(read(this), UtcNow, Observations), Package));
        }
        public Task<bool> SendAsync(PlannedTap action, AutomationSnapshot source, int revision, CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); if (RefuseSend) return Task.FromResult(false); Inputs.Add(action); if (TimeJumpOnSend) UtcNow = UtcNow.AddMinutes(11); if (CancelOnSend) Cancellation.Cancel(); ct.ThrowIfCancellationRequested(); return Task.FromResult(true); }
        public Task DelayAsync(TimeSpan delay, CancellationToken ct)
        { Delays.Add(delay); UtcNow += delay; if (CancelOnDelay) Cancellation.Cancel(); ct.ThrowIfCancellationRequested(); return Task.CompletedTask; }
        public void Report(string message) { }
        public void Record(string stage, ScreenObservation frame, PlannedTap action)
        {
            Stages.Add(stage);
            if (stage != "intent") return;
            if (FailIntent) throw new IOException("Synthetic storage failure");
            if (CancelOnIntent) Cancellation.Cancel();
        }
    }
}
