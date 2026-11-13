using System.Diagnostics;
using System.Runtime.InteropServices;
using SnailCommander.Core.Automation;
using SnailCommander.Core.Interaction;

namespace SnailCommander;

public sealed partial class MainForm
{
    private readonly PictureBox _gameImage = MakePicture();
    private readonly Label _gameStatus = Theme.TextLabel("Live game view — enable manual control to click or drag", 10, color: Theme.Secondary);
    private readonly ModernButton _manualControl = new("Enable manual control", true) { Dock = DockStyle.Fill };
    private bool _gameControlEnabled, _gameInputBusy;
    private CancellationTokenSource? _gameControlCancellation;
    private Task _gameInputTask = Task.CompletedTask;
    private DateTimeOffset _gameFrameAt;
    private string _gameFrameDevice = "";
    private (Point Pixel, string Device, Size ImageSize, DateTimeOffset At)? _gamePointerDown;
    private (int Pid, DateTime Started)? _minimizedEmulator;
    private bool GamePageVisible => _pages.TryGetValue("Game", out var page) && page.Visible;

    private void BuildGamePanel()
    {
        var page = CreatePage("Game");
        var layout = Theme.Table((SizeType.Absolute, 50), (SizeType.Percent, 100), (SizeType.Absolute, 52));
        page.Controls.Add(layout);
        var bar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        bar.Controls.Add(_manualControl, 0, 0);
        var back = new ModernButton("Android Back") { Dock = DockStyle.Fill };
        back.Click += (_, _) => QueueGameInput(null, null, 0, back: true);
        bar.Controls.Add(back, 1, 0);
        var minimize = new ModernButton("Minimize / show BlueStacks") { Dock = DockStyle.Fill };
        minimize.Click += (_, _) => ToggleEmulatorWindow();
        bar.Controls.Add(minimize, 2, 0);
        layout.Controls.Add(bar, 0, 0);
        layout.Controls.Add(_gameImage, 0, 1);
        layout.Controls.Add(_gameStatus, 0, 2);
        _manualControl.Click += async (_, _) =>
        {
            _manualControl.Enabled = false;
            try
            {
                if (_gameControlEnabled) await StopManualControlAsync();
                else
                {
                    await StopAutomationAsync();
                    if (IsDisposed || Disposing) return;
                    await StopManualControlAsync();
                    _gameControlCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
                    _gameControlEnabled = true;
                    _manualControl.Text = "Disable manual control";
                    _gameImage.Cursor = Cursors.Hand;
                    if (!_liveEnabled) await ToggleLiveAsync();
                    _liveTimer.Interval = 500;
                    await CaptureLiveFrameAsync();
                    _gameStatus.Text = "Manual control enabled — click to tap, drag to swipe. Automation is stopped.";
                }
            }
            catch (Exception ex) { if (!IsDisposed) _gameStatus.Text = ex.Message; }
            finally { if (!IsDisposed) _manualControl.Enabled = true; }
        };
        _gameImage.MouseDown += (_, e) =>
        {
            _gamePointerDown = null;
            if (e.Button != MouseButtons.Left || !CanControlGame()) return;
            if (GameViewport.Map(_gameImage.ClientSize.Width, _gameImage.ClientSize.Height, _gameImage.Image!.Width, _gameImage.Image.Height, e.X, e.Y) is { } point)
                _gamePointerDown = (new Point(point.X, point.Y), _gameFrameDevice, _gameImage.Image.Size, DateTimeOffset.UtcNow);
        };
        _gameImage.MouseUp += (_, e) =>
        {
            var start = _gamePointerDown; _gamePointerDown = null;
            if (e.Button != MouseButtons.Left || start is null || !CanControlGame() || start.Value.Device != _gameFrameDevice || start.Value.ImageSize != _gameImage.Image!.Size) return;
            if (GameViewport.Map(_gameImage.ClientSize.Width, _gameImage.ClientSize.Height, _gameImage.Image.Width, _gameImage.Image.Height, e.X, e.Y) is not { } end) return;
            int duration = (int)Math.Clamp((DateTimeOffset.UtcNow - start.Value.At).TotalMilliseconds, 100, 1500);
            QueueGameInput(start.Value.Pixel, new Point(end.X, end.Y), duration);
        };
    }

    private bool CanControlGame() => _gameControlEnabled && !AutomationActive && !_gameInputBusy && _liveEnabled
        && GamePageVisible && _gameImage.Image is not null && _gameFrameDevice == _selectedDevice
        && DateTimeOffset.UtcNow - _gameFrameAt <= TimeSpan.FromSeconds(2);

    private void QueueGameInput(Point? start, Point? end, int duration, bool back = false)
    {
        if (!CanControlGame()) { _gameStatus.Text = "Enable manual control and wait for a fresh live frame first."; return; }
        _gameInputBusy = true;
        _gameInputTask = SendGameInputAsync(start, end, duration, back, _gameFrameDevice, _gameImage.Image!.Size, _gameControlCancellation!.Token);
    }

    private async Task SendGameInputAsync(Point? start, Point? end, int duration, bool back, string device, Size size, CancellationToken ct)
    {
        bool acquired = false;
        try
        {
            await _captureGate.WaitAsync(ct); acquired = true;
            string adb = GetAdb();
            if (await ForegroundPackageAsync(adb, device, ct) != AutomationPolicy.GamePackage)
                throw new InvalidOperationException("Super Snail must be in the foreground. Press Start automation on Home to open it.");
            ct.ThrowIfCancellationRequested();
            if (!_gameControlEnabled || AutomationActive || device != _selectedDevice || _gameImage.Image?.Size != size
                || DateTimeOffset.UtcNow - _gameFrameAt > TimeSpan.FromSeconds(2))
                throw new InvalidOperationException("View changed or became stale. Wait for the live frame and try again.");
            string[] command;
            string Number(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (back) command = new[] { "-s", device, "shell", "input", "keyevent", "4" };
            else if (start is { } a && end is { } b)
            {
                bool swipe = Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) >= 12;
                command = swipe ? new[] { "-s", device, "shell", "input", "swipe", Number(a.X), Number(a.Y), Number(b.X), Number(b.Y), Number(duration) }
                    : new[] { "-s", device, "shell", "input", "tap", Number(b.X), Number(b.Y) };
            }
            else return;
            await AdbService.RunCancelableAsync(adb, ct, command);
            if (!IsDisposed && !Disposing)
            {
                string gesture = back ? "Back" : start is { } first && end is { } last && Math.Abs(first.X - last.X) + Math.Abs(first.Y - last.Y) >= 12 ? "swipe" : "tap";
                Log("Manual game " + gesture + " sent.");
                _gameStatus.Text = "Manual input sent — waiting for the live view.";
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) { if (!IsDisposed && !Disposing) _gameStatus.Text = ex.Message; }
        finally
        {
            if (acquired) _captureGate.Release();
            _gameInputBusy = false;
            _gameFrameAt = DateTimeOffset.MinValue; // A second gesture needs a post-input screenshot.
            if (!IsDisposed && !Disposing) await CaptureLiveFrameAsync();
        }
    }

    private async Task StopManualControlAsync()
    {
        _gameControlEnabled = false;
        _gamePointerDown = null;
        var cancellation = _gameControlCancellation;
        _gameControlCancellation = null;
        cancellation?.Cancel();
        await _gameInputTask;
        cancellation?.Dispose();
        if (IsDisposed || Disposing || _gameControlCancellation is not null || _gameControlEnabled) return;
        _manualControl.Text = "Enable manual control";
        _gameImage.Cursor = Cursors.Default;
        _gameStatus.Text = "Live view — enable manual control to click or drag.";
    }

    private void PresentGameFrame(Image frame, string device)
    {
        if (!GamePageVisible) return;
        var old = _gameImage.Image;
        _gameImage.Image = new Bitmap(frame);
        old?.Dispose();
        _gameFrameAt = DateTimeOffset.UtcNow;
        _gameFrameDevice = device;
    }

    private void ToggleEmulatorWindow()
    {
        try
        {
            if (_minimizedEmulator is { } saved)
            {
                RestoreEmulatorWindow();
                _gameStatus.Text = "BlueStacks restored.";
                return;
            }
            var players = Process.GetProcessesByName("HD-Player");
            try
            {
                var visible = players.Where(p => p.MainWindowHandle != IntPtr.Zero).ToArray();
                if (visible.Length != 1) throw new InvalidOperationException("Multiple or no BlueStacks windows found. Minimize the intended instance manually.");
                var player = visible[0];
                _minimizedEmulator = (player.Id, player.StartTime);
                if (!ShowWindowAsync(player.MainWindowHandle, 6)) { _minimizedEmulator = null; throw new InvalidOperationException("Windows could not minimize BlueStacks."); }
                _gameStatus.Text = "BlueStacks minimized — use the game here. It still runs in the background.";
            }
            finally { foreach (var player in players) player.Dispose(); }
        }
        catch (Exception ex) { _gameStatus.Text = ex.Message; }
    }

    private void RestoreEmulatorWindow()
    {
        if (_minimizedEmulator is not { } saved) return;
        _minimizedEmulator = null;
        try
        {
            using var player = Process.GetProcessById(saved.Pid);
            if (!player.HasExited && player.StartTime == saved.Started && player.MainWindowHandle != IntPtr.Zero)
                ShowWindowAsync(player.MainWindowHandle, 9);
        }
        catch (ArgumentException) { }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindowAsync(IntPtr window, int command);
}
