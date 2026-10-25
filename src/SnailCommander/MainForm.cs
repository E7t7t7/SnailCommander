using System.Diagnostics;

namespace SnailCommander;

/// <summary>
/// Snail Commander v0.4: local-first read-only observation workspace.
/// No gameplay taps, swipes, in-game spending, or automated gameplay.
/// </summary>
public sealed class MainForm : Form
{
    private readonly LocalPreferences _preferences = LocalPreferences.Load();
    private readonly Dictionary<string, Panel> _pages = new();
    private readonly Dictionary<string, ModernButton> _nav = new();
    private readonly List<Button> _actionButtons = new();
    private readonly Panel _pageHost = new() { Dock = DockStyle.Fill, BackColor = Theme.Background };
    private readonly Label _pageTitle = Theme.TextLabel("Overview", 21, true);
    private readonly Label _footer = Theme.TextLabel("Ready • Read-only observation mode", 9, color: Theme.Secondary);
    private readonly Label _deviceMetric = Theme.TextLabel("Not connected", 15, true);
    private readonly Label _captureMetric = Theme.TextLabel("None yet", 15, true);
    private readonly Label _resolutionMetric = Theme.TextLabel("—", 15, true);
    private readonly Label _quickStatus = Theme.TextLabel("Connect to BlueStacks to begin.", 10, color: Theme.Secondary);
    private readonly Label _overviewCaption = Theme.TextLabel("No capture yet. Connect a device and select Capture screen.", 9, color: Theme.Secondary);
    private readonly Label _archiveDetails = Theme.TextLabel("Select a capture to inspect its metadata.", 10, color: Theme.Secondary);
    private readonly PictureBox _latestImage = MakePicture();
    private readonly PictureBox _archiveImage = MakePicture();
    private readonly TextBox _adbPath = Theme.Field();
    private readonly TextBox _endpoint = Theme.Field();
    private readonly ComboBox _devicesQuick = Theme.DropDown();
    private readonly ComboBox _devicesSettings = Theme.DropDown();
    private readonly ComboBox _captureLabel = Theme.DropDown();
    private readonly ListView _captureList = new();
    private readonly TextBox _overviewLog = MakeLog();
    private readonly TextBox _activityLog = MakeLog();
    private List<CaptureRecord> _records = new();
    private CaptureRecord? _selectedCapture;
    private string _selectedDevice = "";
    private bool _syncingDevices;
    private bool _busy;
    private int _recordCount;
    private string _lastImagePath = "";

    private static readonly string LogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SnailCommander", "Logs", "session.log");

    public MainForm()
    {
        Text = "Snail Commander  •  v0.4.0";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1080, 720);
        Size = new Size(1350, 850);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 10);
        DoubleBuffered = true;

        BuildShell();
        BuildOverview();
        BuildCaptures();
        BuildConnection();
        BuildActivity();

        _adbPath.Text = File.Exists(_preferences.AdbPath)
            ? _preferences.AdbPath : AdbService.DiscoverAdb() ?? "";
        _endpoint.Text = string.IsNullOrWhiteSpace(_preferences.Endpoint)
            ? "127.0.0.1:5555" : _preferences.Endpoint;

        _devicesQuick.SelectedIndexChanged += (_, _) => SyncDevice(_devicesQuick);
        _devicesSettings.SelectedIndexChanged += (_, _) => SyncDevice(_devicesSettings);
        LoadCaptureHistory();
        SelectPage("Overview");
        Log("Snail Commander v0.4.0 launched. Read-only mode: no gameplay input.");
        Shown += async (_, _) => await ExecuteAsync(InitializeConnectionAsync);
    }

    private static PictureBox MakePicture() => new()
    {
        Dock = DockStyle.Fill,
        BackColor = Color.FromArgb(7, 12, 19),
        SizeMode = PictureBoxSizeMode.Zoom,
        Margin = new Padding(1)
    };

    private static TextBox MakeLog() => new()
    {
        Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
        ScrollBars = ScrollBars.Vertical, WordWrap = false,
        BackColor = Theme.Card, ForeColor = Theme.Secondary,
        Font = new Font("Consolas", 9.2f),
        BorderStyle = BorderStyle.None,
        Margin = new Padding(2)
    };

    private void BuildShell()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 2 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 216));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var sidebar = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Sidebar, Padding = new Padding(12) };
        root.Controls.Add(sidebar, 0, 0);

        var brand = new Panel { Dock = DockStyle.Top, Height = 101 };
        var logo = new Label
        {
            Text = "SC", BackColor = Theme.AccentDark, ForeColor = Theme.Accent,
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Bounds = new Rectangle(8, 17, 51, 51)
        };
        brand.Controls.Add(logo);
        var product = Theme.TextLabel("SNAIL", 14, true);
        product.SetBounds(70, 16, 110, 26);
        product.Dock = DockStyle.None;
        brand.Controls.Add(product);
        var suffix = Theme.TextLabel("COMMANDER", 10, true, Theme.Accent);
        suffix.SetBounds(70, 42, 118, 24);
        suffix.Dock = DockStyle.None;
        brand.Controls.Add(suffix);
        sidebar.Controls.Add(brand);

        var sidebarFooter = new Panel { Dock = DockStyle.Bottom, Height = 78 };
        var state = Theme.TextLabel("●  READ ONLY", 9, true, Theme.Accent);
        state.Dock = DockStyle.Top;
        state.Height = 26;
        sidebarFooter.Controls.Add(state);
        var version = Theme.TextLabel("v0.4.0   •   Local-first", 9, color: Theme.Secondary);
        version.Dock = DockStyle.Top;
        version.Height = 27;
        sidebarFooter.Controls.Add(version);
        sidebar.Controls.Add(sidebarFooter);

        var navStack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = false,
            BackColor = Theme.Sidebar, Padding = new Padding(0, 8, 0, 0)
        };
        sidebar.Controls.Add(navStack);
        navStack.BringToFront();
        foreach (var (name, glyph) in new[]
        {
            ("Overview", "◉"), ("Captures", "▣"),
            ("Connection", "⌁"), ("Activity", "≡")
        })
        {
            var button = new ModernButton($"   {glyph}   {name}")
            {
                Width = 184, Height = 49,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(3, 3, 3, 6),
                BackColor = Theme.Sidebar
            };
            button.Click += (_, _) => SelectPage(name);
            navStack.Controls.Add(button);
            _nav[name] = button;
        }

        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1,
            Padding = new Padding(19, 9, 19, 8), BackColor = Theme.Background
        };
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 33));
        root.Controls.Add(main, 1, 0);

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
        _pageTitle.Dock = DockStyle.Fill;
        header.Controls.Add(_pageTitle);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right, Width = 330,
            FlowDirection = FlowDirection.RightToLeft, WrapContents = false,
            Padding = new Padding(0, 12, 0, 0)
        };
        var capture = MakeButton("Capture screen", CaptureAsync, true);
        capture.Width = 160;
        capture.Height = 42;
        var update = MakeButton("Update GitHub", CheckForUpdatesAsync);
        update.Width = 149;
        update.Height = 42;
        actions.Controls.Add(capture);
        actions.Controls.Add(update);
        header.Controls.Add(actions);

        main.Controls.Add(header, 0, 0);
        main.Controls.Add(_pageHost, 0, 1);
        var footer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6, 2, 6, 0) };
        footer.Controls.Add(_footer);
        main.Controls.Add(footer, 0, 2);
    }

    private void BuildOverview()
    {
        var page = CreatePage("Overview");
        var table = Theme.Table((SizeType.Absolute, 105), (SizeType.Percent, 100), (SizeType.Absolute, 163));
        page.Controls.Add(table);

        var metrics = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        for (int i = 0; i < 3; i++) metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
        metrics.Controls.Add(MetricCard("ADB CONNECTION", _deviceMetric, "Select an authorized emulator"), 0, 0);
        metrics.Controls.Add(MetricCard("CAPTURE LIBRARY", _captureMetric, "Saved to this PC only"), 1, 0);
        metrics.Controls.Add(MetricCard("LATEST SCREEN", _resolutionMetric, "Auto-detected image size"), 2, 0);
        table.Controls.Add(metrics, 0, 0);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        table.Controls.Add(body, 0, 1);

        var previewCard = new SurfacePanel();
        previewCard.Dock = DockStyle.Fill;
        var previewGrid = Theme.Table((SizeType.Absolute, 37), (SizeType.Percent, 100), (SizeType.Absolute, 39));
        previewGrid.Controls.Add(Theme.TextLabel("LATEST GAME CAPTURE", 10, true, Theme.Secondary), 0, 0);
        previewGrid.Controls.Add(_latestImage, 0, 1);
        previewGrid.Controls.Add(_overviewCaption, 0, 2);
        previewCard.Controls.Add(previewGrid);
        body.Controls.Add(previewCard, 0, 0);

        var quickCard = new SurfacePanel { Dock = DockStyle.Fill };
        var quick = Theme.Table(
            (SizeType.Absolute, 43),
            (SizeType.Absolute, 46),
            (SizeType.Absolute, 30),
            (SizeType.Absolute, 42),
            (SizeType.Absolute, 54),
            (SizeType.Absolute, 47),
            (SizeType.Absolute, 42),
            (SizeType.Percent, 100));
        quick.Controls.Add(Theme.TextLabel("QUICK CONTROL", 11, true), 0, 0);
        quick.Controls.Add(_quickStatus, 0, 1);
        quick.Controls.Add(Theme.TextLabel("CONNECTED DEVICE", 9, true, Theme.Secondary), 0, 2);
        quick.Controls.Add(_devicesQuick, 0, 3);
        quick.Controls.Add(MakeButton("Capture game screen", CaptureAsync, true), 0, 4);
        quick.Controls.Add(MakeButton("Connect / refresh", () => ExecuteConnectAsync()), 0, 5);
        quick.Controls.Add(MakeButton("Open capture folder", OpenCapturesAsync), 0, 6);
        var tip = Theme.TextLabel(
            "TIP   Open Super Snail before capturing.\n\n" +
            "Captures are stored locally. You can categorize them on the Captures page to build a reference library.",
            9, color: Theme.Secondary);
        tip.TextAlign = ContentAlignment.TopLeft;
        tip.Padding = new Padding(0, 16, 0, 0);
        quick.Controls.Add(tip, 0, 7);
        quickCard.Controls.Add(quick);
        body.Controls.Add(quickCard, 1, 0);

        var activity = new SurfacePanel { Dock = DockStyle.Fill, Padding = new Padding(16, 8, 16, 10) };
        var activityLayout = Theme.Table((SizeType.Absolute, 31), (SizeType.Percent, 100));
        activityLayout.Controls.Add(Theme.TextLabel("RECENT ACTIVITY", 10, true, Theme.Secondary), 0, 0);
        activityLayout.Controls.Add(_overviewLog, 0, 1);
        activity.Controls.Add(activityLayout);
        table.Controls.Add(activity, 0, 2);
    }

    private SurfacePanel MetricCard(string eyebrow, Label metric, string hint)
    {
        var surface = new SurfacePanel { Dock = DockStyle.Fill, Padding = new Padding(17, 12, 12, 8) };
        var layout = Theme.Table(
            (SizeType.Absolute, 23), (SizeType.Percent, 100), (SizeType.Absolute, 22));
        layout.Controls.Add(Theme.TextLabel(eyebrow, 9, true, Theme.Secondary), 0, 0);
        layout.Controls.Add(metric, 0, 1);
        layout.Controls.Add(Theme.TextLabel(hint, 9, color: Theme.Secondary), 0, 2);
        surface.Controls.Add(layout);
        return surface;
    }

    private void BuildCaptures()
    {
        var page = CreatePage("Captures");
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        page.Controls.Add(body);

        var gallery = new SurfacePanel { Dock = DockStyle.Fill };
        var galleryLayout = Theme.Table(
            (SizeType.Absolute, 40), (SizeType.Percent, 100), (SizeType.Absolute, 51));
        galleryLayout.Controls.Add(Theme.TextLabel("SCREENSHOT LIBRARY", 11, true), 0, 0);
        _captureList.Dock = DockStyle.Fill;
        _captureList.View = View.Details;
        _captureList.FullRowSelect = true;
        _captureList.MultiSelect = false;
        _captureList.HideSelection = false;
        _captureList.BackColor = Theme.Card;
        _captureList.ForeColor = Theme.Text;
        _captureList.BorderStyle = BorderStyle.None;
        _captureList.Font = new Font("Segoe UI", 9.5f);
        _captureList.Columns.Add("Captured", 145);
        _captureList.Columns.Add("Screen label", 170);
        _captureList.Columns.Add("Resolution", 100);
        _captureList.SelectedIndexChanged += (_, _) => OnCaptureSelected();
        galleryLayout.Controls.Add(_captureList, 0, 1);
        var galleryButtons = TwoButtons(
            MakeButton("Refresh list", () => { LoadCaptureHistory(); return Task.CompletedTask; }),
            MakeButton("Open folder", OpenCapturesAsync));
        galleryLayout.Controls.Add(galleryButtons, 0, 2);
        gallery.Controls.Add(galleryLayout);
        body.Controls.Add(gallery, 0, 0);

        var detail = new SurfacePanel { Dock = DockStyle.Fill };
        var detailLayout = Theme.Table(
            (SizeType.Absolute, 39),
            (SizeType.Percent, 100),
            (SizeType.Absolute, 70),
            (SizeType.Absolute, 32),
            (SizeType.Absolute, 43),
            (SizeType.Absolute, 49));
        detailLayout.Controls.Add(Theme.TextLabel("CAPTURE INSPECTOR", 11, true), 0, 0);
        detailLayout.Controls.Add(_archiveImage, 0, 1);
        _archiveDetails.Padding = new Padding(2, 4, 2, 3);
        detailLayout.Controls.Add(_archiveDetails, 0, 2);
        detailLayout.Controls.Add(Theme.TextLabel("LABEL THIS SCREEN (MANUAL)", 9, true, Theme.Secondary), 0, 3);
        foreach (var label in CaptureStore.Labels) _captureLabel.Items.Add(label);
        _captureLabel.SelectedIndex = 0;
        detailLayout.Controls.Add(_captureLabel, 0, 4);
        detailLayout.Controls.Add(MakeButton("Save screen label", SaveCaptureLabelAsync, true), 0, 5);
        detail.Controls.Add(detailLayout);
        body.Controls.Add(detail, 1, 0);
    }

    private void BuildConnection()
    {
        var page = CreatePage("Connection");
        var scroller = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoScroll = true,
            FlowDirection = FlowDirection.TopDown, WrapContents = false
        };
        page.Controls.Add(scroller);
        var connection = new SurfacePanel
        {
            Width = 800, Height = 540, Padding = new Padding(22), Margin = new Padding(5)
        };
        var controls = Theme.Table(
            (SizeType.Absolute, 48), (SizeType.Absolute, 42),
            (SizeType.Absolute, 26), (SizeType.Absolute, 38), (SizeType.Absolute, 52),
            (SizeType.Absolute, 29), (SizeType.Absolute, 38), (SizeType.Absolute, 52),
            (SizeType.Absolute, 29), (SizeType.Absolute, 38), (SizeType.Absolute, 52),
            (SizeType.Percent, 100));
        controls.Controls.Add(Theme.TextLabel("BLUESTACKS CONNECTION", 15, true), 0, 0);
        controls.Controls.Add(Theme.TextLabel(
            "Connects locally through Android Debug Bridge. No account password or remote service.",
            10, color: Theme.Secondary), 0, 1);
        controls.Controls.Add(Theme.TextLabel("ADB EXECUTABLE", 9, true, Theme.Secondary), 0, 2);
        controls.Controls.Add(_adbPath, 0, 3);
        controls.Controls.Add(TwoButtons(
            MakeButton("Detect ADB", DetectAdbAsync),
            MakeButton("Browse executable", BrowseAdbAsync)), 0, 4);
        controls.Controls.Add(Theme.TextLabel("LOCAL ENDPOINT  •  DEFAULT 127.0.0.1:5555", 9, true, Theme.Secondary), 0, 5);
        controls.Controls.Add(_endpoint, 0, 6);
        controls.Controls.Add(MakeButton("Connect to BlueStacks", ExecuteConnectAsync, true), 0, 7);
        controls.Controls.Add(Theme.TextLabel("AUTHORIZED DEVICES", 9, true, Theme.Secondary), 0, 8);
        controls.Controls.Add(_devicesSettings, 0, 9);
        controls.Controls.Add(TwoButtons(
            MakeButton("Refresh devices", RefreshDevicesCoreAsync),
            MakeButton("Open capture folder", OpenCapturesAsync)), 0, 10);
        var note = Theme.TextLabel(
            "Your device list may contain both 127.0.0.1:5555 and emulator-5554. " +
            "Choose the entry displaying the correct Super Snail instance. " +
            "The desktop window size is not necessarily the Android screenshot resolution.",
            10, color: Theme.Secondary);
        note.TextAlign = ContentAlignment.TopLeft;
        note.Padding = new Padding(1, 12, 1, 0);
        controls.Controls.Add(note, 0, 11);
        connection.Controls.Add(controls);
        scroller.Controls.Add(connection);
        scroller.ClientSizeChanged += (_, _) =>
            connection.Width = Math.Max(530, scroller.ClientSize.Width - 30);
        connection.Width = Math.Max(530, scroller.ClientSize.Width - 30);

        var maintenance = new SurfacePanel { Width = connection.Width, Height = 138, Padding = new Padding(22) };
        var ml = Theme.Table((SizeType.Absolute, 34), (SizeType.Absolute, 40), (SizeType.Percent, 100));
        ml.Controls.Add(Theme.TextLabel("PRIVACY & MAINTENANCE", 11, true), 0, 0);
        ml.Controls.Add(MakeButton("Remove legacy GitHub token", () =>
        {
            GitHubTokenStore.Clear();
            Log("Legacy GitHub token removed from Windows Credential Manager.");
            return Task.CompletedTask;
        }), 0, 1);
        ml.Controls.Add(Theme.TextLabel("Public updates require no GitHub token.", 9, color: Theme.Secondary), 0, 2);
        maintenance.Controls.Add(ml);
        scroller.Controls.Add(maintenance);
        scroller.ClientSizeChanged += (_, _) =>
            maintenance.Width = Math.Max(530, scroller.ClientSize.Width - 30);
    }

    private void BuildActivity()
    {
        var page = CreatePage("Activity");
        var panel = new SurfacePanel { Dock = DockStyle.Fill };
        var layout = Theme.Table(
            (SizeType.Absolute, 40), (SizeType.Percent, 100), (SizeType.Absolute, 52));
        layout.Controls.Add(Theme.TextLabel("DIAGNOSTIC LOG", 11, true), 0, 0);
        layout.Controls.Add(_activityLog, 0, 1);
        layout.Controls.Add(TwoButtons(
            MakeButton("Open log folder", () =>
            {
                var dir = Path.GetDirectoryName(LogFile)!;
                Directory.CreateDirectory(dir);
                Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
                return Task.CompletedTask;
            }),
            MakeButton("Clear this view", () =>
            {
                _activityLog.Clear();
                _overviewLog.Clear();
                Log("Activity view cleared (disk log retained).");
                return Task.CompletedTask;
            })), 0, 2);
        panel.Controls.Add(layout);
        page.Controls.Add(panel);
    }

    private Panel CreatePage(string name)
    {
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
        _pages.Add(name, page);
        _pageHost.Controls.Add(page);
        return page;
    }

    private void SelectPage(string name)
    {
        _pageHost.SuspendLayout();
        foreach (var (pageName, page) in _pages) page.Visible = pageName == name;
        var active = _pages[name];
        active.BringToFront();
        foreach (var (pageName, button) in _nav)
        {
            button.BackColor = pageName == name ? Theme.AccentDark : Theme.Sidebar;
            button.ForeColor = pageName == name ? Theme.Accent : Theme.Secondary;
        }
        _pageTitle.Text = name switch
        {
            "Overview" => "Command overview",
            "Captures" => "Capture library",
            "Connection" => "Connection settings",
            "Activity" => "Activity & diagnostics",
            _ => name
        };
        _pageHost.ResumeLayout();
    }

    private static TableLayoutPanel TwoButtons(Button first, Button second)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        first.Dock = DockStyle.Fill;
        second.Dock = DockStyle.Fill;
        row.Controls.Add(first, 0, 0);
        row.Controls.Add(second, 1, 0);
        return row;
    }

    private ModernButton MakeButton(string title, Func<Task> action, bool primary = false)
    {
        var button = new ModernButton(title, primary) { Dock = DockStyle.Fill };
        button.Click += async (_, _) => await ExecuteAsync(action);
        _actionButtons.Add(button);
        return button;
    }

    private async Task ExecuteAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        foreach (var button in _actionButtons) button.Enabled = false;
        try { await action(); }
        catch (Exception e)
        {
            SetStatus("Error: " + e.Message);
            Log("ERROR: " + e.Message);
        }
        finally
        {
            foreach (var button in _actionButtons) button.Enabled = true;
            _busy = false;
        }
    }

    private async Task InitializeConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(_adbPath.Text))
        {
            SetStatus("ADB not found. Choose Connection → Browse executable.");
            Log("ADB executable not detected. Browse manually in Connection.");
            return;
        }
        try
        {
            await AdbService.ConnectAsync(GetAdb(), _endpoint.Text.Trim());
            await RefreshDevicesCoreAsync();
        }
        catch (Exception ex)
        {
            Log("Automatic ADB connection not established: " + ex.Message);
            SetStatus("Check Connection settings to connect BlueStacks.");
        }
    }

    private string GetAdb()
    {
        var path = _adbPath.Text.Trim().Trim('"');
        if (!File.Exists(path))
            throw new FileNotFoundException("Select an existing HD-Adb.exe or adb.exe on the Connection page.", path);
        return path;
    }

    private async Task DetectAdbAsync()
    {
        var path = AdbService.DiscoverAdb();
        if (path is null)
            throw new FileNotFoundException("No ADB executable found. Use Browse executable.");
        _adbPath.Text = path;
        SavePreferences();
        Log("ADB detected: " + path);
        await RefreshDevicesCoreAsync();
    }

    private Task BrowseAdbAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select HD-Adb.exe or adb.exe",
            Filter = "Android Debug Bridge|HD-Adb.exe;adb.exe|Executables|*.exe"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _adbPath.Text = dialog.FileName;
            SavePreferences();
            Log("ADB path selected.");
        }
        return Task.CompletedTask;
    }

    private async Task ExecuteConnectAsync()
    {
        var endpoint = _endpoint.Text.Trim();
        if (!System.Net.IPAddress.TryParse(endpoint.Split(':')[0], out var address)
            || !System.Net.IPAddress.IsLoopback(address)
            || !int.TryParse(endpoint.Split(':').LastOrDefault(), out int port)
            || port < 1 || port > 65535)
            throw new ArgumentException("Enter a local ADB address such as 127.0.0.1:5555.");

        var output = await AdbService.ConnectAsync(GetAdb(), endpoint);
        Log(output);
        SavePreferences();
        await RefreshDevicesCoreAsync();
    }

    private async Task RefreshDevicesCoreAsync()
    {
        var entries = await AdbService.GetDevicesAsync(GetAdb());
        _syncingDevices = true;
        try
        {
            _devicesQuick.Items.Clear();
            _devicesSettings.Items.Clear();
            foreach (var serial in entries)
            {
                _devicesQuick.Items.Add(serial);
                _devicesSettings.Items.Add(serial);
            }
            var preferred = new[] { _selectedDevice, _preferences.PreferredDevice, _endpoint.Text.Trim() }
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x) && entries.Contains(x));
            _selectedDevice = preferred ?? entries.FirstOrDefault() ?? "";
            if (!string.IsNullOrWhiteSpace(_selectedDevice))
            {
                _devicesQuick.SelectedItem = _selectedDevice;
                _devicesSettings.SelectedItem = _selectedDevice;
            }
        }
        finally { _syncingDevices = false; }

        if (entries.Count == 0)
        {
            _deviceMetric.Text = "Offline";
            _quickStatus.Text = "No connected devices. Open Connection settings.";
            SetStatus("BlueStacks device not detected.");
            Log("No ADB devices found. Check ADB enabled and configured endpoint.");
        }
        else
        {
            _deviceMetric.Text = entries.Count == 1 ? "1 device online" : $"{entries.Count} devices online";
            _quickStatus.Text = "Connected: " + _selectedDevice;
            SetStatus("Connected to local Android device.");
            Log("ADB devices: " + string.Join(", ", entries));
        }
        SavePreferences();
    }

    private void SyncDevice(ComboBox source)
    {
        if (_syncingDevices || source.SelectedItem is not string serial) return;
        _syncingDevices = true;
        try
        {
            _selectedDevice = serial;
            _devicesQuick.SelectedItem = serial;
            _devicesSettings.SelectedItem = serial;
            _quickStatus.Text = "Selected: " + serial;
        }
        finally { _syncingDevices = false; }
        SavePreferences();
    }

    private void SavePreferences()
    {
        try
        {
            _preferences.AdbPath = _adbPath.Text.Trim();
            _preferences.Endpoint = _endpoint.Text.Trim();
            _preferences.PreferredDevice = _selectedDevice;
            _preferences.Save();
        }
        catch (Exception ex) { Log("Could not save preferences: " + ex.Message); }
    }

    private async Task CaptureAsync()
    {
        if (string.IsNullOrWhiteSpace(_selectedDevice))
            await ExecuteConnectAsync();
        if (string.IsNullOrWhiteSpace(_selectedDevice))
            throw new InvalidOperationException("No ADB device selected. Open Connection settings.");

        byte[] png = await AdbService.CaptureAsync(GetAdb(), _selectedDevice);
        var record = await CaptureStore.SaveAsync(png, _selectedDevice);
        LoadCaptureHistory();
        PresentCapture(record);
        SelectPage("Overview");
        SetStatus("Saved screenshot: " + record.FileName);
        Log($"Captured {record.Dimensions} from {_selectedDevice} • {record.FileName}");
    }

    private void LoadCaptureHistory()
    {
        _records = CaptureStore.LoadRecent();
        _recordCount = _records.Count;
        _captureMetric.Text = $"{_recordCount} snapshots";

        _captureList.BeginUpdate();
        try
        {
            _captureList.Items.Clear();
            foreach (var record in _records)
            {
                var row = new ListViewItem(record.CapturedAt.ToLocalTime().ToString("MMM d, h:mm tt"))
                {
                    Tag = record
                };
                row.SubItems.Add(record.Label);
                row.SubItems.Add(record.Dimensions);
                _captureList.Items.Add(row);
            }
        }
        finally { _captureList.EndUpdate(); }

        if (_records.Count > 0)
        {
            var newest = _records[0];
            _captureMetric.Text = $"{_recordCount} snapshots";
            if (string.IsNullOrWhiteSpace(_lastImagePath)) PresentCapture(newest);
        }
    }

    private void OnCaptureSelected()
    {
        if (_captureList.SelectedItems.Count == 0) return;
        if (_captureList.SelectedItems[0].Tag is CaptureRecord record)
        {
            _selectedCapture = record;
            _archiveDetails.Text = $"{record.CapturedAt.LocalDateTime:MMM d, yyyy • h:mm:ss tt}\n" +
                                   $"{record.Dimensions}  •  {record.Device}";
            _captureLabel.SelectedItem = CaptureStore.Labels.Contains(record.Label)
                ? record.Label : "Unclassified";
            LoadPreview(_archiveImage, CaptureStore.ImagePath(record));
        }
    }

    private async Task SaveCaptureLabelAsync()
    {
        if (_selectedCapture is null)
            throw new InvalidOperationException("Choose a screenshot from the list first.");
        if (_captureLabel.SelectedItem is not string label)
            throw new InvalidOperationException("Select a screen label.");

        await CaptureStore.SaveLabelAsync(_selectedCapture, label);
        Log($"Labeled {_selectedCapture.FileName} as {label}");
        _selectedCapture = _selectedCapture with { Label = label };
        LoadCaptureHistory();
        foreach (ListViewItem item in _captureList.Items)
        {
            if (item.Tag is CaptureRecord record && record.FileName == _selectedCapture.FileName)
            {
                item.Selected = true;
                item.EnsureVisible();
                break;
            }
        }
    }

    private void PresentCapture(CaptureRecord capture)
    {
        _lastImagePath = CaptureStore.ImagePath(capture);
        _resolutionMetric.Text = capture.Dimensions;
        _overviewCaption.Text = $"{capture.CapturedAt.LocalDateTime:MMM d, h:mm:ss tt}    •    {capture.Label}    •    {capture.Device}";
        LoadPreview(_latestImage, _lastImagePath);
    }

    private static void LoadPreview(PictureBox target, string path)
    {
        try
        {
            using var image = Image.FromFile(path);
            var next = new Bitmap(image);
            var old = target.Image;
            target.Image = next;
            old?.Dispose();
        }
        catch (Exception) { /* Missing/corrupt archive entry is not a process failure. */ }
    }

    private Task OpenCapturesAsync()
    {
        Directory.CreateDirectory(CaptureStore.DirectoryPath);
        Process.Start(new ProcessStartInfo(CaptureStore.DirectoryPath) { UseShellExecute = true });
        return Task.CompletedTask;
    }

    private async Task CheckForUpdatesAsync()
    {
        Log("Checking latest public Snail Commander release...");
        var latest = await GitHubUpdates.LatestAsync();
        if (GitHubUpdates.AlreadyInstalled(latest))
        {
            SetStatus("Already running the latest public release.");
            Log("Already up to date: " + latest.CommitSha[..10]);
            return;
        }
        var answer = MessageBox.Show(this,
            $"Snail Commander update available: {latest.CommitSha[..10]}.\n\n" +
            "Download verified release, close Snail Commander, install, then reopen?\n\n" +
            "Local captures, labels, preferences, and logs are stored outside the app folder.",
            "Update Snail Commander", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) { Log("Update cancelled."); return; }
        SetStatus("Downloading and verifying GitHub release...");
        var staging = await GitHubUpdates.DownloadAndPrepareAsync(latest);
        Log("Verified update staged. Exiting to external updater.");
        GitHubUpdates.LaunchInstallerAndExit(staging);
        BeginInvoke(new Action(Close));
    }

    private void SetStatus(string message)
    {
        _footer.Text = message;
    }

    private void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _overviewLog.AppendText(line + Environment.NewLine);
        _activityLog.AppendText(line + Environment.NewLine);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFile)!);
            File.AppendAllText(LogFile, line + Environment.NewLine);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _latestImage.Image?.Dispose();
        _archiveImage.Image?.Dispose();
        base.OnFormClosed(e);
    }
}
