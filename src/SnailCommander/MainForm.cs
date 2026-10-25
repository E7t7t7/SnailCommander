using System.Diagnostics;
using System.Drawing;

namespace SnailCommander;

public sealed class MainForm : Form
{
    private static readonly Color Bg = Color.FromArgb(15, 19, 29);
    private static readonly Color PanelBg = Color.FromArgb(23, 29, 43);
    private static readonly Color InputBg = Color.FromArgb(34, 42, 59);
    private static readonly Color Foreground = Color.FromArgb(232, 239, 248);
    private static readonly Color Muted = Color.FromArgb(158, 173, 193);
    private static readonly Color Accent = Color.FromArgb(69, 145, 241);

    private readonly TextBox _adbPath = new();
    private readonly TextBox _endpoint = new();
    private readonly ComboBox _devices = new();
    private readonly TextBox _log = new();
    private readonly PictureBox _preview = new();
    private readonly Label _status = new();
    private readonly Label _captureLabel = new();
    private readonly List<Button> _buttons = new();

    private static readonly string CaptureDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        "SnailCommander", "Captures");

    private static readonly string LogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SnailCommander", "Logs", "session.log");

    public MainForm()
    {
        Text = "Snail Commander — v0.3.0";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1000, 700);
        Size = new Size(1280, 810);
        BackColor = Bg;
        ForeColor = Foreground;
        Font = new Font("Segoe UI", 10);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(0)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 365));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var sidebar = new Panel { Dock = DockStyle.Fill, BackColor = PanelBg, AutoScroll = true };
        root.Controls.Add(sidebar, 0, 0);

        sidebar.Controls.Add(MakeLabel("SNAIL COMMANDER", 24, 25, 320, 32, 19, Foreground, true));
        sidebar.Controls.Add(MakeLabel("Local diagnostics • Token-free updater v0.3", 24, 62, 320, 27, 10, Muted));

        sidebar.Controls.Add(MakeLabel("ADB EXECUTABLE", 24, 113, 315, 23, 9, Muted, true));
        StyleTextBox(_adbPath, 24, 139, 315);
        _adbPath.Text = AdbService.DiscoverAdb() ?? "";
        sidebar.Controls.Add(_adbPath);

        AddButton(sidebar, "Detect ADB", 24, 177, 151, async () =>
        {
            var adb = AdbService.DiscoverAdb();
            if (adb is null) throw new FileNotFoundException("No ADB executable was found. Use Browse.");
            _adbPath.Text = adb;
            Log("Detected ADB: " + adb);
            await RefreshDevicesCoreAsync();
        });

        AddButton(sidebar, "Browse...", 188, 177, 151, () =>
        {
            using var picker = new OpenFileDialog
            {
                Title = "Select HD-Adb.exe or adb.exe",
                Filter = "ADB executables|HD-Adb.exe;adb.exe|Executable files|*.exe"
            };
            if (picker.ShowDialog(this) == DialogResult.OK)
            {
                _adbPath.Text = picker.FileName;
                Log("ADB executable selected.");
            }
            return Task.CompletedTask;
        });

        sidebar.Controls.Add(MakeLabel("LOCAL ADB ENDPOINT", 24, 235, 315, 23, 9, Muted, true));
        StyleTextBox(_endpoint, 24, 261, 315);
        _endpoint.Text = "127.0.0.1:5555";
        sidebar.Controls.Add(_endpoint);

        AddButton(sidebar, "Connect", 24, 303, 151, async () =>
        {
            var message = await AdbService.ConnectAsync(GetAdb(), _endpoint.Text);
            Log(message);
            await RefreshDevicesCoreAsync();
        });
        AddButton(sidebar, "Refresh devices", 188, 303, 151, RefreshDevicesCoreAsync);

        sidebar.Controls.Add(MakeLabel("CONNECTED DEVICE", 24, 364, 315, 23, 9, Muted, true));
        _devices.SetBounds(24, 390, 315, 32);
        _devices.BackColor = InputBg;
        _devices.ForeColor = Foreground;
        _devices.FlatStyle = FlatStyle.Flat;
        _devices.DropDownStyle = ComboBoxStyle.DropDownList;
        sidebar.Controls.Add(_devices);

        AddButton(sidebar, "Capture screenshot", 24, 450, 315, CaptureAsync, true);
        AddButton(sidebar, "Open capture folder", 24, 500, 315, () =>
        {
            Directory.CreateDirectory(CaptureDirectory);
            Process.Start(new ProcessStartInfo(CaptureDirectory) { UseShellExecute = true });
            return Task.CompletedTask;
        });

        sidebar.Controls.Add(MakeLabel("STATUS", 24, 577, 315, 23, 9, Muted, true));
        _status.SetBounds(24, 604, 315, 50);
        _status.ForeColor = Muted;
        _status.Text = "Waiting for ADB connection";
        sidebar.Controls.Add(_status);

        sidebar.Controls.Add(MakeLabel("GITHUB UPDATES (PUBLIC)", 24, 662, 315, 23, 9, Muted, true));
        sidebar.Controls.Add(MakeLabel("No token or GitHub login required.", 24, 692, 315, 25, 10, Foreground));
        AddButton(sidebar, "Check & install update", 24, 734, 315, CheckForUpdatesAsync, true);
        AddButton(sidebar, "Remove old GitHub token", 24, 784, 315, () =>
        {
            GitHubTokenStore.Clear();
            Log("Legacy GitHub token removed from Windows Credential Manager.");
            return Task.CompletedTask;
        });
        sidebar.Controls.Add(MakeLabel("You can also revoke old tokens on GitHub.", 24, 832, 315, 26, 9, Muted));

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Bg, Padding = new Padding(18) };
        right.RowCount = 4;
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 75));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        root.Controls.Add(right, 1, 0);

        var captureHeader = new Panel { Dock = DockStyle.Fill };
        var title = MakeLabel("Screenshot preview", 4, 2, 330, 36, 16, Foreground, true);
        title.Dock = DockStyle.Left;
        captureHeader.Controls.Add(title);
        var topUpdate = new Button
        {
            Text = "Update from GitHub",
            Dock = DockStyle.Right,
            Width = 185,
            FlatStyle = FlatStyle.Flat,
            BackColor = Accent,
            ForeColor = Foreground,
            Cursor = Cursors.Hand
        };
        topUpdate.FlatAppearance.BorderSize = 0;
        topUpdate.Click += async (_, _) => await ExecuteAsync(CheckForUpdatesAsync);
        _buttons.Add(topUpdate);
        captureHeader.Controls.Add(topUpdate);
        right.Controls.Add(captureHeader, 0, 0);
        _preview.Dock = DockStyle.Fill;
        _preview.BackColor = Color.FromArgb(7, 11, 20);
        _preview.BorderStyle = BorderStyle.FixedSingle;
        _preview.SizeMode = PictureBoxSizeMode.Zoom;
        right.Controls.Add(_preview, 0, 1);

        _captureLabel.Text = "No screenshot captured yet";
        _captureLabel.ForeColor = Muted;
        _captureLabel.Dock = DockStyle.Fill;
        _captureLabel.TextAlign = ContentAlignment.MiddleLeft;
        right.Controls.Add(_captureLabel, 0, 2);

        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.ScrollBars = ScrollBars.Vertical;
        _log.Dock = DockStyle.Fill;
        _log.BackColor = PanelBg;
        _log.ForeColor = Foreground;
        _log.BorderStyle = BorderStyle.None;
        _log.Font = new Font("Consolas", 9);
        right.Controls.Add(_log, 0, 3);

        Log("Dashboard started. This build does not send gameplay inputs.");
        if (!string.IsNullOrWhiteSpace(_adbPath.Text)) Log("ADB detected: " + _adbPath.Text);
        else Log("ADB not located automatically. Browse for HD-Adb.exe.");
    }

    private static Label MakeLabel(string text, int x, int y, int w, int h, int fontSize, Color color, bool bold = false)
        => new()
        {
            Text = text, Left = x, Top = y, Width = w, Height = h,
            ForeColor = color,
            Font = new Font("Segoe UI", fontSize, bold ? FontStyle.Bold : FontStyle.Regular)
        };

    private static void StyleTextBox(TextBox field, int x, int y, int width)
    {
        field.SetBounds(x, y, width, 32);
        field.BackColor = InputBg;
        field.ForeColor = Foreground;
        field.BorderStyle = BorderStyle.FixedSingle;
    }

    private void AddButton(Control parent, string title, int x, int y, int width, Func<Task> action, bool prominent = false)
    {
        var btn = new Button
        {
            Text = title, Left = x, Top = y, Width = width, Height = 37,
            FlatStyle = FlatStyle.Flat,
            BackColor = prominent ? Accent : InputBg,
            ForeColor = Foreground,
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += async (_, _) => await ExecuteAsync(action);
        _buttons.Add(btn);
        parent.Controls.Add(btn);
    }

    private async Task ExecuteAsync(Func<Task> action)
    {
        foreach (var button in _buttons) button.Enabled = false;
        try { await action(); }
        catch (Exception ex)
        {
            _status.Text = "Error: " + ex.Message;
            Log("ERROR: " + ex.Message);
        }
        finally { foreach (var button in _buttons) button.Enabled = true; }
    }

    private string GetAdb()
    {
        var path = _adbPath.Text.Trim().Trim('"');
        if (!File.Exists(path))
            throw new FileNotFoundException("Select an existing HD-Adb.exe or adb.exe first.", path);
        return path;
    }

    private async Task RefreshDevicesCoreAsync()
    {
        var serials = await AdbService.GetDevicesAsync(GetAdb());
        var previous = _devices.SelectedItem as string;
        _devices.Items.Clear();
        foreach (var device in serials) _devices.Items.Add(device);

        if (serials.Count > 0)
        {
            _devices.SelectedItem = previous is not null && serials.Contains(previous) ? previous : serials[0];
            _status.Text = $"Connected: {serials.Count} device(s)";
            Log("Connected device(s): " + string.Join(", ", serials));
        }
        else
        {
            _status.Text = "No authorized ADB devices found";
            Log("No devices. Enable ADB in BlueStacks and check the configured port.");
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        Log("Checking latest public Snail Commander release...");
        var newest = await GitHubUpdates.LatestAsync();
        if (GitHubUpdates.AlreadyInstalled(newest))
        {
            _status.Text = "You already have the latest release.";
            Log("Already up to date: " + newest.CommitSha[..10]);
            return;
        }

        var answer = MessageBox.Show(this,
            $"A newer Snail Commander release is available (commit {newest.CommitSha[..10]}).\n\n" +
            "Download it, close Snail Commander, install the update, and reopen automatically?\n\n" +
            "Your screenshots and logs will be kept.",
            "Update from GitHub", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (answer != DialogResult.Yes)
        {
            Log("Update cancelled.");
            return;
        }

        _status.Text = "Downloading public GitHub release...";
        Log("Downloading public release and verifying package SHA-256.");
        var staging = await GitHubUpdates.DownloadAndPrepareAsync(newest);
        Log("Update verified and staged. Closing app for external installer...");
        GitHubUpdates.LaunchInstallerAndExit(staging);
        BeginInvoke(new Action(Close));
    }

    private async Task CaptureAsync()
    {
        if (_devices.SelectedItem is not string serial)
            throw new InvalidOperationException("Connect and select an ADB device first.");

        var bytes = await AdbService.CaptureAsync(GetAdb(), serial);
        Directory.CreateDirectory(CaptureDirectory);
        var filePath = Path.Combine(CaptureDirectory, $"snail-{DateTime.Now:yyyyMMdd-HHmmss-fff}.png");
        await File.WriteAllBytesAsync(filePath, bytes);

        using (var stream = new MemoryStream(bytes))
        using (var source = Image.FromStream(stream))
        {
            var previous = _preview.Image;
            _preview.Image = new Bitmap(source);
            previous?.Dispose();
            _captureLabel.Text = $"Saved {source.Width} × {source.Height} • {Path.GetFileName(filePath)}";
        }

        _status.Text = "Screenshot captured successfully";
        Log("Saved screenshot: " + filePath);
    }

    private void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _log.AppendText(line + Environment.NewLine);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFile)!);
            File.AppendAllText(LogFile, line + Environment.NewLine);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
