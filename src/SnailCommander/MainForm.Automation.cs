using System.Text.Json;
using System.Text.RegularExpressions;
using SnailCommander.Core.Automation;
using SnailCommander.Core.Observation;
using SnailCommander.Observation;

namespace SnailCommander;

public sealed partial class MainForm
{
    private readonly ModernButton _startAutomation = new("Start automation", true) { Dock = DockStyle.Fill };
    private readonly ModernButton _pauseAutomation = new("Pause") { Dock = DockStyle.Fill, Enabled = false };
    private readonly ModernButton _stopAutomation = new("Stop") { Dock = DockStyle.Fill, Enabled = false };
    private readonly ModernButton _headerStop = new("Stop automation") { Dock = DockStyle.Fill, Enabled = false };
    private readonly Label _automationStatus = Theme.TextLabel("Ready — press Start automation", 12, true, Theme.Accent);
    private CancellationTokenSource? _automationCancellation;
    private Task? _automationTask;
    private bool _automationPaused;
    private int _automationRevision;
    private bool AutomationActive => _automationTask is { IsCompleted: false };

    private void BuildAutomationControls(TableLayoutPanel panel)
    {
        panel.Controls.Add(Theme.TextLabel("ONE BUTTON START", 11, true), 0, 0);
        panel.Controls.Add(_startAutomation, 0, 1);
        panel.Controls.Add(TwoButtons(_pauseAutomation, _stopAutomation), 0, 2);
        panel.Controls.Add(_automationStatus, 0, 3);
        var capabilities = Theme.TextLabel("Open Game to play inside this window; BlueStacks can stay minimized. Automation handles recognized Start, free reward OK, and update notices.\n\nOther gameplay needs your attention. No spending, upgrading, or cloning routines yet.\n\nPause stops new actions. Stop cancels the run. Use Advanced for OCR, account evidence, and troop planning.", 10, color: Theme.Secondary);
        capabilities.TextAlign = ContentAlignment.TopLeft;
        panel.Controls.Add(capabilities, 0, 4);
        _startAutomation.Click += async (_, _) =>
        {
            if (AutomationActive || _busy) return;
            _startAutomation.Enabled = false;
            await StopManualControlAsync();
            if (IsDisposed || Disposing) return;
            _automationCancellation?.Dispose();
            _automationCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            _automationPaused = false;
            _startAutomation.Enabled = false;
            _pauseAutomation.Enabled = _stopAutomation.Enabled = true;
            _headerStop.Enabled = true;
            _pauseAutomation.Text = "Pause";
            _automationTask = RunAutomationAsync(_automationCancellation.Token);
        };
        _pauseAutomation.Click += (_, _) =>
        {
            _automationPaused = !_automationPaused;
            _automationRevision++;
            _pauseAutomation.Text = _automationPaused ? "Resume" : "Pause";
            AutomationStatus(_automationPaused ? "Paused — no new actions" : "Resuming — observing again");
        };
        _stopAutomation.Click += (_, _) => { _automationCancellation?.Cancel(); AutomationStatus("Stopping…"); };
        _headerStop.Click += (_, _) => { _automationCancellation?.Cancel(); AutomationStatus("Stopping…"); };
    }

    private void AutomationStatus(string message)
    {
        if (IsDisposed || Disposing) return;
        _automationStatus.Text = message;
        SetStatus("Automation • " + message);
        Log("Automation: " + message);
    }

    private async Task StopAutomationAsync()
    {
        _automationCancellation?.Cancel();
        if (_automationTask is { } task) await task;
    }

    private async Task RunAutomationAsync(CancellationToken ct)
    {
        string adb = _adbPath.Text.Trim();
        string serial = "";
        try
        {
            AutomationStatus("Connecting to BlueStacks…");
            if (!File.Exists(adb)) adb = AdbService.DiscoverAdb() ?? throw new InvalidOperationException("ADB not found. Choose it in Settings.");
            string endpoint = _endpoint.Text.Trim();
            if (!Regex.IsMatch(endpoint, @"^127\.0\.0\.1:\d{1,5}$")) throw new InvalidOperationException("Choose a local BlueStacks endpoint in Settings.");
            try { await AdbService.RunCancelableAsync(adb, ct, "connect", endpoint); }
            catch (InvalidOperationException) { }
            var devices = SnailCommander.Core.Devices.AdbDeviceParser.ConnectedSerials(await AdbService.RunCancelableAsync(adb, ct, "devices"));
            if (!devices.Contains(endpoint))
            {
                string player = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "BlueStacks_nxt", "HD-Player.exe");
                if (!File.Exists(player)) throw new InvalidOperationException("BlueStacks is not installed. Open your emulator and retry.");
                string configuration = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "BlueStacks_nxt", "bluestacks.conf");
                string port = endpoint[(endpoint.IndexOf(':') + 1)..];
                var instances = File.Exists(configuration)
                    ? Regex.Matches(await File.ReadAllTextAsync(configuration, ct), "(?m)^bst\\.instance\\.([A-Za-z0-9_]+)\\.adb_port=\"" + port + "\"\\r?$").Select(m => m.Groups[1].Value).Distinct().ToArray()
                    : Array.Empty<string>();
                if (instances.Length != 1) throw new InvalidOperationException("Open the configured BlueStacks instance, then retry. Its port could not be matched uniquely.");
                var launchInfo = new System.Diagnostics.ProcessStartInfo(player) { UseShellExecute = true, WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden };
                launchInfo.ArgumentList.Add("--instance"); launchInfo.ArgumentList.Add(instances[0]);
                await WaitAutomationAsync(ct);
                System.Diagnostics.Process.Start(launchInfo);
                for (int attempt = 0; attempt < 24 && !devices.Contains(endpoint); attempt++)
                {
                    await Task.Delay(2500, ct);
                    await WaitAutomationAsync(ct);
                    try { await AdbService.RunCancelableAsync(adb, ct, "connect", endpoint); } catch (InvalidOperationException) { }
                    devices = SnailCommander.Core.Devices.AdbDeviceParser.ConnectedSerials(await AdbService.RunCancelableAsync(adb, ct, "devices"));
                }
            }
            if (!devices.Contains(endpoint)) throw new InvalidOperationException("BlueStacks did not connect within the startup deadline.");
            serial = endpoint;
            _adbPath.Text = adb;
            _selectedDevice = serial;
            await RefreshDevicesCoreAsync();
            if (_selectedDevice != serial) throw new InvalidOperationException("Device changed during startup.");
            await WaitAutomationAsync(ct);
            var installed = await AdbService.RunCancelableAsync(adb, ct, "-s", serial, "shell", "pm", "path", AutomationPolicy.GamePackage);
            if (!installed.Contains("package:")) throw new InvalidOperationException("Super Snail is not installed in this instance.");
            await WaitAutomationAsync(ct);
            bool launch = await ForegroundPackageAsync(adb, serial, ct) != AutomationPolicy.GamePackage;
            await WaitAutomationAsync(ct);
            if (launch)
                await AdbService.RunCancelableAsync(adb, ct, "-s", serial, "shell", "monkey", "-p", AutomationPolicy.GamePackage, "-c", "android.intent.category.LAUNCHER", "1");
            if (!_liveEnabled) await ToggleLiveAsync();
            var observer = new ObservationService(Path.Combine(AppPaths.DataDirectory, "Observations"), retryUnknownAtLargerScale: true);
            int completed = 0;
            int unknownObservations = 0;
            while (completed < 20)
            {
                await WaitAutomationAsync(ct);
                if (_selectedDevice != serial) throw new InvalidOperationException("Selected device changed; run stopped.");
                AutomationStatus("Observing Super Snail…");
                var before = await ReadAutomationFrameAsync(observer, adb, serial, ct);
                string package = await ForegroundPackageAsync(adb, serial, ct);
                var plan = AutomationPolicy.Plan(before, package, serial, DateTimeOffset.UtcNow);
                if (plan is null)
                {
                    if (++unknownObservations >= 12)
                        throw new InvalidOperationException("Needs attention: no supported action on this screen. Continue in the game, then press Start again.");
                    AutomationStatus($"Waiting for a supported screen • {completed} verified actions");
                    await Task.Delay(5000, ct);
                    continue;
                }
                await WaitAutomationAsync(ct);
                int revision = _automationRevision;
                var fresh = await ReadAutomationFrameAsync(observer, adb, serial, ct);
                var current = AutomationPolicy.Plan(fresh, await ForegroundPackageAsync(adb, serial, ct), serial, DateTimeOffset.UtcNow);
                if (current is null || current.Action != plan.Action) continue;
                await WaitAutomationAsync(ct);
                if (_selectedDevice != serial || revision != _automationRevision || DateTimeOffset.UtcNow - fresh.CapturedAt > TimeSpan.FromSeconds(30)) continue;
                JournalAutomation("intent", fresh, current);
                ct.ThrowIfCancellationRequested();
                // No await between the pause/device gate and dispatch. All UI state is owned by this UI thread.
                if (_automationPaused) continue;
                await AdbService.RunCancelableAsync(adb, ct, "-s", serial, "shell", "input", "tap", current.X.ToString(System.Globalization.CultureInfo.InvariantCulture), current.Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
                JournalAutomation("sent", fresh, current);
                AutomationStatus("Verifying " + current.Action + "…");
                bool verified = false;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    await Task.Delay(3000, ct);
                    var after = await ReadAutomationFrameAsync(observer, adb, serial, ct);
                    if (AutomationPolicy.Verified(current, fresh, after, await ForegroundPackageAsync(adb, serial, ct))) { verified = true; break; }
                }
                JournalAutomation(verified ? "verified" : "unverified-no-retry", fresh, current);
                if (!verified) throw new InvalidOperationException("Needs attention: action outcome could not be verified. No retry was sent.");
                completed++;
                unknownObservations = 0;
                AutomationStatus($"Verified {current.Action} • {completed} completed");
            }
            AutomationStatus("Session limit reached • 20 verified actions");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { AutomationStatus("Stopped"); }
        catch (Exception ex) { AutomationStatus(ex.Message); }
        finally
        {
            if (!IsDisposed && !Disposing)
            {
                _startAutomation.Enabled = true;
                _pauseAutomation.Enabled = _stopAutomation.Enabled = false;
                _headerStop.Enabled = false;
                _pauseAutomation.Text = "Pause";
            }
        }
    }

    private async Task WaitAutomationAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        while (_automationPaused) await Task.Delay(150, ct);
    }

    private static async Task<string> ForegroundPackageAsync(string adb, string serial, CancellationToken ct)
    {
        var output = await AdbService.RunCancelableAsync(adb, ct, "-s", serial, "shell", "dumpsys", "window", "windows");
        var focus = Regex.Match(output, @"mCurrentFocus=.*?\s([a-zA-Z0-9_.]+)/");
        return focus.Success ? focus.Groups[1].Value : "";
    }

    private async Task<ScreenObservation> ReadAutomationFrameAsync(ObservationService observer, string adb, string serial, CancellationToken ct)
    {
        await _captureGate.WaitAsync(ct);
        byte[] png;
        try { png = await AdbService.CaptureAsync(adb, serial, ct); }
        finally { _captureGate.Release(); }
        var result = await observer.ObserveAsync(png, serial, true, ct, ObservationOrigin.LiveAdb)
            ?? throw new InvalidOperationException("Automation observation unavailable.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        result = await Task.Run(() => AutomationButtonReader.ReadAsync(png, result, deadline.Token), ct);
        ct.ThrowIfCancellationRequested();
        if (!IsDisposed && !Disposing) { PresentObservation(result, "Automation"); RefreshObservationHistory(); }
        return result;
    }

    private static void JournalAutomation(string stage, ScreenObservation frame, PlannedTap action)
    {
        string folder = Path.Combine(AppPaths.DataDirectory, "Automation");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "last-action.json");
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(new { stage, at = DateTimeOffset.UtcNow, frame.Id, frame.Device, action }));
        File.Move(path + ".tmp", path, true);
        // This checkpoint is audit evidence only. Startup never replays an unfinished input.
    }

    private void BuildAdvanced()
    {
        var page = CreatePage("Advanced");
        var stack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        page.Controls.Add(stack);
        foreach (string name in new[] { "Account", "Intelligence", "Minion Command", "Captures" })
        {
            var button = new ModernButton(name) { Width = 300, Height = 48 };
            button.Click += (_, _) => SelectPage(name);
            stack.Controls.Add(button);
        }
        _liveSpeed.Width = 180;
        stack.Controls.Add(Theme.TextLabel("Live preview interval", 10));
        stack.Controls.Add(_liveSpeed);
        var preview = MakeLiveToggle("Pause live preview");
        preview.Dock = DockStyle.None; preview.Width = 300; preview.Height = 42;
        stack.Controls.Add(preview);
        var capture = MakeButton("Save snapshot", CaptureAsync);
        capture.Dock = DockStyle.None; capture.Width = 300; capture.Height = 42;
        stack.Controls.Add(capture);
    }
}
