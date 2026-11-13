namespace SnailCommander;

public sealed partial class MainForm
{
    private readonly System.Windows.Forms.Timer _updateTimer = new() { Interval = 6 * 60 * 60 * 1000 };
    private readonly SemaphoreSlim _updateGate = new(1, 1);
    private readonly Label _updateStatus = Theme.TextLabel("Checking public releases...", 11, true, Theme.Accent);
    private readonly TextBox _releaseNotes = MakeLog();
    private readonly CheckBox _autoDownload = new() { Text = "Automatically download verified updates; install when I choose", Dock = DockStyle.Fill, ForeColor = Theme.Text };
    private readonly NumericUpDown _backupRetention = new() { Minimum = 1, Maximum = 10, Value = 3, Dock = DockStyle.Fill, BackColor = Theme.Raised, ForeColor = Theme.Text };
    private ModernButton? _installUpdate;
    private UpdateInfo? _availableUpdate;
    private string? _stagedUpdate;

    private void BuildUpdates()
    {
        var page = CreatePage("Updates");
        var layout = Theme.Table((SizeType.Absolute, 44), (SizeType.Absolute, 48),
            (SizeType.Absolute, 40), (SizeType.Absolute, 40), (SizeType.Percent, 100), (SizeType.Absolute, 48));
        page.Controls.Add(layout);
        layout.Controls.Add(Theme.TextLabel($"Installed v{GitHubUpdates.CurrentVersion.ToString(3)} • commit {ShortCommit(GitHubUpdates.CurrentBuildSha)}", 12, true), 0, 0);
        layout.Controls.Add(_updateStatus, 0, 1);
        _autoDownload.Checked = _preferences.AutoDownloadUpdates;
        _autoDownload.CheckedChanged += (_, _) =>
        {
            _preferences.AutoDownloadUpdates = _autoDownload.Checked;
            SavePreferences();
            if (_autoDownload.Checked) _ = RefreshUpdateStatusAsync();
        };
        layout.Controls.Add(_autoDownload, 0, 2);
        var retention = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        retention.ColumnStyles.Add(new(SizeType.Absolute, 230));
        retention.ColumnStyles.Add(new(SizeType.Absolute, 70));
        retention.ColumnStyles.Add(new(SizeType.Percent, 100));
        retention.Controls.Add(Theme.TextLabel("Previous installations to retain", 9), 0, 0);
        _backupRetention.Value = Math.Clamp(_preferences.UpdateBackupRetention, 1, 10);
        _backupRetention.ValueChanged += (_, _) => { _preferences.UpdateBackupRetention = (int)_backupRetention.Value; SavePreferences(); };
        retention.Controls.Add(_backupRetention, 1, 0);
        retention.Controls.Add(MakeButton("Open update history", () =>
        {
            var dir = Path.Combine(DataDirectory, "Backups");
            Directory.CreateDirectory(dir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dir) { UseShellExecute = true });
            return Task.CompletedTask;
        }), 2, 0);
        layout.Controls.Add(retention, 0, 3);
        layout.Controls.Add(TextCard("PUBLIC RELEASE NOTES", _releaseNotes), 0, 4);
        _releaseNotes.WordWrap = true;
        _installUpdate = new ModernButton("Install available update", true) { Enabled = false };
        _installUpdate.Click += async (_, _) => await ExecuteAsync(InstallUpdateAsync);
        layout.Controls.Add(TwoButtons(MakeButton("Check GitHub now", RefreshUpdateStatusAsync), _installUpdate), 0, 5);
        _updateTimer.Tick += async (_, _) => await RefreshUpdateStatusAsync();
    }

    private static string ShortCommit(string sha) => sha.Length >= 10 ? sha[..10] : "local build";

    private async Task RefreshUpdateStatusAsync()
    {
        if (IsDisposed || Disposing || _lifetime.IsCancellationRequested) return;
        if (!await _updateGate.WaitAsync(0)) return;
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            deadline.CancelAfter(TimeSpan.FromSeconds(45));
            var latest = await GitHubUpdates.LatestAsync(deadline.Token);
            if (IsDisposed || Disposing) return;
            _releaseNotes.Text = latest.Changelog;
            if (!GitHubUpdates.IsUpdateAvailable(latest))
            {
                _availableUpdate = null;
                _updateStatus.Text = $"No newer release • latest public build {ShortCommit(latest.CommitSha)} • checked {DateTime.Now:HH:mm}";
                return;
            }
            if (_availableUpdate?.CommitSha != latest.CommitSha) _stagedUpdate = null;
            _availableUpdate = latest;
            _updateStatus.Text = $"Update available: {(latest.Version is null ? "" : "v" + latest.Version + " • ")}{ShortCommit(latest.CommitSha)}";
            if (_autoDownload.Checked && _stagedUpdate is null)
            {
                _updateStatus.Text = "Downloading and verifying update...";
                _stagedUpdate = await GitHubUpdates.DownloadAndPrepareAsync(latest, _lifetime.Token);
                if (!IsDisposed) _updateStatus.Text = "Verified update ready to install. Installation closes and reopens the app.";
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing)
            {
                _updateStatus.Text = "Update check failed: " + ex.Message;
                Log("Public update check: " + ex.Message);
            }
        }
        finally
        {
            if (!IsDisposed && _installUpdate is not null) _installUpdate.Enabled = _availableUpdate is not null;
            _updateGate.Release();
        }
    }

    private async Task InstallUpdateAsync()
    {
        if (_availableUpdate is null) return;
        if (!await _updateGate.WaitAsync(0, _lifetime.Token)) return;
        try
        {
            _installUpdate!.Enabled = false;
            _updateStatus.Text = "Downloading and verifying update...";
            _stagedUpdate ??= await GitHubUpdates.DownloadAndPrepareAsync(_availableUpdate, _lifetime.Token);
            Log("Verified release staged. Closing to external updater; failed startup will trigger rollback.");
            await StopAutomationAsync();
            _liveTimer.Stop();
            GitHubUpdates.LaunchInstallerAndExit(_stagedUpdate, _preferences.UpdateBackupRetention);
            _lifetime.Cancel();
            BeginInvoke(new Action(Close));
        }
        finally
        {
            if (!IsDisposed && _installUpdate is not null) _installUpdate.Enabled = _availableUpdate is not null;
            _updateGate.Release();
        }
    }
}
