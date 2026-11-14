using SnailCommander.Core.Observation;

namespace SnailCommander.Core.Automation;

public sealed record AutomationSnapshot(ScreenObservation Frame, string Package);
public sealed record AutomationRunResult(string Outcome, string Reason, int VerifiedActions, int Inputs, int Observations);
public interface IAutomationPort
{
    DateTimeOffset UtcNow { get; }
    string SelectedDevice { get; }
    int Revision { get; }
    Task WaitReadyAsync(CancellationToken ct);
    Task<AutomationSnapshot> ObserveAsync(CancellationToken ct);
    Task<bool> SendAsync(PlannedTap action, AutomationSnapshot source, int revision, CancellationToken ct);
    Task DelayAsync(TimeSpan delay, CancellationToken ct);
    void Report(string message);
    void Record(string stage, ScreenObservation frame, PlannedTap action);
}

/// <summary>Serializable conservative rate budget. Reserve before dispatch, never refund an uncertain input.</summary>
public sealed class RecoveryBudget
{
    public List<DateTimeOffset> Attempts { get; init; } = new();
    public int Used(DateTimeOffset now) => Attempts.Count(t => t > now - TimeSpan.FromMinutes(15));
    public bool Reserve(DateTimeOffset now)
    {
        Attempts.RemoveAll(t => t <= now - TimeSpan.FromMinutes(15));
        if (Used(now) >= 3) return false;
        Attempts.Add(now);
        return true;
    }
}

/// <summary>Finite, source-checked execution. No input is replayed on restart or repeated merely because verification failed.</summary>
public sealed class AutomationRunner
{
    private readonly IAutomationPort _port;
    private readonly string _device;
    private readonly RecoveryBudget _budget;
    public AutomationRunner(IAutomationPort port, string device, RecoveryBudget budget)
        => (_port, _device, _budget) = (port, device, budget);

    public async Task<AutomationRunResult> RunAsync(CancellationToken ct)
    {
        int completed = 0, inputs = 0, observations = 0, unknown = 0;
        var deadline = _port.UtcNow.AddMinutes(10);
        (ScreenObservation Frame, PlannedTap Action)? pending = null;
        AutomationRunResult End(string outcome, string reason) => new(outcome, reason, completed, inputs, observations);
        async Task<AutomationSnapshot> Observe()
        {
            ct.ThrowIfCancellationRequested();
            if (_port.SelectedDevice != _device) throw new InvalidOperationException("Selected device changed.");
            if (observations >= 100 || _port.UtcNow >= deadline) throw new SessionLimitException();
            observations++;
            return await _port.ObserveAsync(ct);
        }
        PlannedTap? Plan(AutomationSnapshot snapshot) => AutomationPolicy.Plan(snapshot.Frame, snapshot.Package, _device, _port.UtcNow);
        try
        {
            while (completed < 20)
            {
                await _port.WaitReadyAsync(ct);
                var before = await Observe();
                var plan = Plan(before);
                if (plan is null)
                {
                    if (AutomationPolicy.IsLiveHome(before.Frame, before.Package, _device, _port.UtcNow))
                    {
                        unknown = 0;
                        _port.Report($"Home observed • monitoring supported dialogs • {completed} verified actions");
                        await _port.DelayAsync(TimeSpan.FromSeconds(10), ct);
                        continue;
                    }
                    if (++unknown >= 12) return End("needs-attention", "No supported action on this screen.");
                    _port.Report($"Waiting for a supported screen • {completed} verified actions");
                    await _port.DelayAsync(TimeSpan.FromSeconds(5), ct);
                    continue;
                }
                if (plan.Action == SafeAction.RetryLogin)
                {
                    int used = _budget.Used(_port.UtcNow);
                    if (used >= 3) return End("needs-attention", "Login recovery limit reached: three attempts per fifteen minutes.");
                    int seconds = new[] { 5, 15, 30 }[used];
                    _port.Report($"Login recovery {used + 1}/3 • waiting {seconds}s before rechecking");
                    await _port.DelayAsync(TimeSpan.FromSeconds(seconds), ct);
                }
                await _port.WaitReadyAsync(ct);
                int revision = _port.Revision;
                var fresh = await Observe();
                var current = Plan(fresh);
                if (current is null || current.Action != plan.Action) continue;
                await _port.WaitReadyAsync(ct);
                if (_port.SelectedDevice != _device) return End("needs-attention", "Selected device changed.");
                if (revision != _port.Revision || Plan(fresh) != current) continue;
                if (current.Action == SafeAction.RetryLogin && !_budget.Reserve(_port.UtcNow))
                    return End("needs-attention", "Login recovery limit reached.");
                _port.Record("intent", fresh.Frame, current); // Persistence failure prevents dispatch.
                ct.ThrowIfCancellationRequested();
                inputs++;
                pending = (fresh.Frame, current);
                if (!await _port.SendAsync(current, fresh, revision, ct))
                {
                    inputs--; pending = null;
                    _port.Record("preflight-changed-no-input", fresh.Frame, current);
                    continue;
                }
                _port.Record("sent", fresh.Frame, current);
                _port.Report("Verifying " + current.Action + "…");
                bool verified = false, explicitLoginFailure = false;
                int checks = current.Action is SafeAction.Login or SafeAction.RetryLogin ? 10 : 3;
                for (int check = 0; check < checks; check++)
                {
                    await _port.DelayAsync(TimeSpan.FromSeconds(3), ct);
                    var after = await Observe();
                    if (after.Package != AutomationPolicy.GamePackage || after.Frame.Device != _device
                        || after.Frame.Origin != ObservationOrigin.LiveAdb || after.Frame.CapturedAt <= fresh.Frame.CapturedAt
                        || after.Frame.CapturedAt > _port.UtcNow || _port.UtcNow - after.Frame.CapturedAt > TimeSpan.FromSeconds(30))
                        return End("needs-attention", "Verification lost the bound live game context.");
                    explicitLoginFailure = AutomationPolicy.IsLoginFailure(after.Frame);
                    if (current.Action == SafeAction.Login && explicitLoginFailure) break;
                    if (AutomationPolicy.Verified(current, fresh.Frame, after.Frame, after.Package)) { verified = true; break; }
                }
                if (!verified)
                {
                    _port.Record(explicitLoginFailure ? "login-error-observed" : "unverified-no-retry", fresh.Frame, current);
                    pending = null;
                    if (explicitLoginFailure && current.Action is SafeAction.Login or SafeAction.RetryLogin)
                    {
                        _port.Report("Login error observed • recovery requires a fresh recognized Retry button");
                        unknown = 0;
                        continue;
                    }
                    return End("needs-attention", "Action outcome could not be verified. No retry was sent.");
                }
                _port.Record("verified", fresh.Frame, current);
                pending = null;
                completed++; unknown = 0;
                _port.Report($"Verified {current.Action} transition • {completed} completed");
            }
            return End("complete", "Session limit reached: twenty verified actions.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            if (pending is { } input) { try { _port.Record("cancelled-unverified", input.Frame, input.Action); } catch (Exception) { } }
            return End("cancelled", pending is null ? "Stopped" : "Stopped; last input outcome is unverified.");
        }
        catch (SessionLimitException)
        {
            if (pending is { } input)
            {
                try { _port.Record("session-limit-unverified", input.Frame, input.Action); } catch (Exception) { }
                return End("needs-attention", "Session limit interrupted verification; no replay or retry was sent.");
            }
            return End("complete", "Observation/session time limit reached; press Start to reobserve.");
        }
        catch (Exception ex)
        {
            if (pending is { } input) { try { _port.Record("dispatch-or-verification-uncertain", input.Frame, input.Action); } catch (Exception) { } }
            return End("needs-attention", ex.Message);
        }
    }

    private sealed class SessionLimitException : Exception { }
}
