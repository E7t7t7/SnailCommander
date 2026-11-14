using SnailCommander.Core.Automation;
using SnailCommander.Core.Observation;

namespace SnailCommander;

/// <summary>Production ADB execution adapter; UI coordination is injected so the same adapter can be integration-tested without desktop input.</summary>
internal sealed class AdbAutomationPort : IAutomationPort
{
    private readonly string _adb, _device;
    private readonly SemaphoreSlim _gate;
    private readonly Func<CancellationToken, Task<ScreenObservation>> _observe;
    private readonly Func<CancellationToken, Task> _wait;
    private readonly Func<string> _selected;
    private readonly Func<int> _revision;
    private readonly Func<bool> _paused;
    private readonly Action<string> _report;
    private readonly Action<string, ScreenObservation, PlannedTap> _record;
    public AdbAutomationPort(string adb, string device, SemaphoreSlim gate,
        Func<CancellationToken, Task<ScreenObservation>> observe, Func<CancellationToken, Task> wait,
        Func<string> selected, Func<int> revision, Func<bool> paused, Action<string> report,
        Action<string, ScreenObservation, PlannedTap> record)
        => (_adb, _device, _gate, _observe, _wait, _selected, _revision, _paused, _report, _record)
            = (adb, device, gate, observe, wait, selected, revision, paused, report, record);
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public string SelectedDevice => _selected();
    public int Revision => _revision();
    public Task WaitReadyAsync(CancellationToken ct) => _wait(ct);
    public Task DelayAsync(TimeSpan delay, CancellationToken ct) => Task.Delay(delay, ct);
    public void Report(string message) => _report(message);
    public void Record(string stage, ScreenObservation frame, PlannedTap action) => _record(stage, frame, action);
    public async Task<AutomationSnapshot> ObserveAsync(CancellationToken ct)
    {
        var frame = await _observe(ct);
        return new(frame, await AdbService.GetForegroundPackageAsync(_adb, _device, ct));
    }
    public async Task<bool> SendAsync(PlannedTap action, AutomationSnapshot source, int revision, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            string package = await AdbService.GetForegroundPackageAsync(_adb, _device, ct);
            ct.ThrowIfCancellationRequested();
            if (_paused() || revision != Revision || SelectedDevice != _device
                || AutomationPolicy.Plan(source.Frame, package, _device, UtcNow) != action) return false;
            await AdbService.RunCancelableAsync(_adb, ct, "-s", _device, "shell", "input", "tap",
                action.X.ToString(System.Globalization.CultureInfo.InvariantCulture), action.Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return true;
        }
        finally { _gate.Release(); }
    }
}
