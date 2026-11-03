using System.Text;
using System.Text.Json;
using SnailCommander.Core.Minions;
using SnailCommander.Core.Observation;
using SnailCommander.Observation;

namespace SnailCommander;

public sealed partial class MainForm
{
    private static string DataDirectory => AppPaths.DataDirectory;
    private readonly CancellationTokenSource _lifetime = new();
    private ObservationService? _observer;
    private readonly TextBox _recognizedText = MakeLog();
    private readonly TextBox _observationDetails = MakeLog();
    private readonly Label _recognitionStatus = Theme.TextLabel("Waiting for local OCR", 12, true, Theme.Accent);
    private readonly ComboBox _observationHistory = Theme.DropDown();
    private string _lastObservationError = "";
    private ScreenObservation? _displayedObservation;
    private readonly TextBox _minionResults = MakeLog();
    private readonly Label _minionStatus = Theme.TextLabel("No account imported", 11, true, Theme.Accent);
    private readonly ComboBox _troopGoal = Theme.DropDown();
    private readonly ComboBox _troopMode = Theme.DropDown();
    private MinionAccount? _minionAccount;

    private ObservationService Observer => _observer ??= new(Path.Combine(DataDirectory, "Observations"));

    private void BuildIntelligence()
    {
        var page = CreatePage("Intelligence");
        var layout = Theme.Table((SizeType.Absolute, 55), (SizeType.Absolute, 44),
            (SizeType.Percent, 100), (SizeType.Absolute, 46), (SizeType.Absolute, 42));
        page.Controls.Add(layout);
        layout.Controls.Add(_recognitionStatus, 0, 0);
        _observationHistory.SelectedIndexChanged += (_, _) =>
        {
            if (_observationHistory.SelectedItem is ObservationChoice choice) PresentObservation(choice.Observation, "Saved observation");
        };
        layout.Controls.Add(_observationHistory, 0, 1);
        var columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        columns.ColumnStyles.Add(new(SizeType.Percent, 50));
        columns.ColumnStyles.Add(new(SizeType.Percent, 50));
        columns.Controls.Add(TextCard("RECOGNIZED TEXT • LOCAL WINDOWS OCR", _recognizedText), 0, 0);
        columns.Controls.Add(TextCard("SCREEN EVIDENCE & FRAMEBUFFER REGIONS", _observationDetails), 1, 0);
        layout.Controls.Add(columns, 0, 2);
        layout.Controls.Add(TwoButtons(MakeButton("Analyze current screen", AnalyzeCurrentScreenAsync, true),
            MakeButton("Analyze image file", AnalyzeImageFileAsync)), 0, 3);
        layout.Controls.Add(TwoButtons(MakeButton("Replay saved observations", ReplayObservationsAsync),
            MakeButton("Export observation JSON", ExportObservationAsync)), 0, 4);
        try
        {
            RefreshObservationHistory();
            if (Observer.Latest is { } previous) PresentObservation(previous, "Previous session");
        }
        catch (Exception ex) { _recognitionStatus.Text = "History unavailable: " + ex.Message; }
    }

    private static Panel TextCard(string heading, TextBox body)
    {
        var panel = new SurfacePanel { Dock = DockStyle.Fill };
        var layout = Theme.Table((SizeType.Absolute, 35), (SizeType.Percent, 100));
        layout.Controls.Add(Theme.TextLabel(heading, 9, true, Theme.Secondary), 0, 0);
        layout.Controls.Add(body, 0, 1);
        panel.Controls.Add(layout);
        return panel;
    }

    private async Task ObserveFrameAsync(byte[] png, string device, bool force = false)
    {
        try
        {
            var result = await Observer.ObserveAsync(png, device, force, _lifetime.Token);
            if (IsDisposed || Disposing || _lifetime.IsCancellationRequested) return;
            if (result is null) return;
            if (!force && device != _selectedDevice) return;
            _lastObservationError = "";
            PresentObservation(result, "Observed");
            RefreshObservationHistory();
            _quickStatus.Text = $"{result.Classification.Kind} • rule score {result.Classification.Confidence:P0}\n" +
                $"{result.Elements.Count} text regions • {result.OcrLanguage}\n" +
                "Local OCR every 10s on changed screens; refresh every 60s. Gameplay execution is not implemented.";
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (IsDisposed || Disposing) return;
            _recognitionStatus.Text = "OCR unavailable: " + ex.Message;
            if (_lastObservationError != ex.Message) { Log("Observation: " + ex.Message); _lastObservationError = ex.Message; }
        }
    }

    private void PresentObservation(ScreenObservation observation, string origin)
    {
        _displayedObservation = observation;
        var classification = observation.Classification;
        _recognitionStatus.Text = $"{origin} {observation.CapturedAt.LocalDateTime:MMM d HH:mm:ss} • {classification.Kind} • rule score {classification.Confidence:P0}";
        _recognizedText.Text = string.IsNullOrWhiteSpace(observation.Text) ? "No readable text. Screen remains unknown." : observation.Text;
        _recognizedText.WordWrap = true;
        _observationDetails.WordWrap = true;
        var details = new StringBuilder()
            .AppendLine($"Device: {observation.Device}")
            .AppendLine($"Frame: {observation.Width} × {observation.Height} ({observation.Orientation})")
            .AppendLine($"Source: {observation.RecognitionSource}")
            .AppendLine($"OCR language: {observation.OcrLanguage}")
            .AppendLine($"Rule set: {classification.RuleVersion}")
            .AppendLine("Rule score is heuristic, not a probability.")
            .AppendLine().AppendLine("Matched evidence:");
        foreach (var evidence in classification.Evidence) details.AppendLine("• " + evidence);
        details.AppendLine().AppendLine("Visible labeled resources (unverified readings):");
        if (observation.Resources.Count == 0) details.AppendLine("None established from this frame.");
        foreach (var resource in observation.Resources) details.AppendLine($"{resource.Name}: {resource.Quantity:N0} — {resource.RawText}");
        details.AppendLine().AppendLine("Text boxes in Android framebuffer pixels:");
        foreach (var region in observation.Elements.Take(250))
            details.AppendLine($"[{region.X:F0},{region.Y:F0} {region.Width:F0}×{region.Height:F0}] {region.Text}");
        _observationDetails.Text = details.ToString();
    }

    private void RefreshObservationHistory()
    {
        _observationHistory.BeginUpdate();
        try
        {
            _observationHistory.Items.Clear();
            foreach (var observation in Observer.Store.LoadRecent(100)) _observationHistory.Items.Add(new ObservationChoice(observation));
        }
        finally { _observationHistory.EndUpdate(); }
    }

    private sealed record ObservationChoice(ScreenObservation Observation)
    {
        public override string ToString() => $"{Observation.CapturedAt.LocalDateTime:MMM d HH:mm:ss} • {Observation.Classification.Kind} • {Observation.Device}";
    }

    private async Task AnalyzeCurrentScreenAsync()
    {
        if (string.IsNullOrWhiteSpace(_selectedDevice)) await ExecuteConnectAsync();
        if (string.IsNullOrWhiteSpace(_selectedDevice)) throw new InvalidOperationException("No connected emulator is available.");
        byte[] png;
        string device = _selectedDevice;
        await _captureGate.WaitAsync(_lifetime.Token);
        try { png = await AdbService.CaptureAsync(GetAdb(), device, _lifetime.Token); }
        finally { _captureGate.Release(); }
        await ObserveFrameAsync(png, device, true);
    }

    private async Task AnalyzeImageFileAsync()
    {
        using var dialog = new OpenFileDialog { Title = "Analyze a local screenshot", Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (new FileInfo(dialog.FileName).Length > 32 * 1024 * 1024) throw new InvalidDataException("Image exceeds 32 MB.");
        await ObserveFrameAsync(await File.ReadAllBytesAsync(dialog.FileName, _lifetime.Token), "File: " + Path.GetFileName(dialog.FileName), true);
    }

    private async Task ReplayObservationsAsync()
    {
        var replayed = await Task.Run(() => ObservationReplay.Run(Observer.Store.LoadRecent(100), new ScreenClassifier()), _lifetime.Token);
        if (replayed.Count == 0) throw new InvalidOperationException("No observations recorded yet.");
        PresentObservation(replayed[^1], $"Replayed {replayed.Count} records; latest");
        Log($"Replayed {replayed.Count} saved OCR records through current classification rules.");
    }

    private async Task ExportObservationAsync()
    {
        if (_displayedObservation is null) throw new InvalidOperationException("Analyze or select an observation first.");
        using var dialog = new SaveFileDialog { Filter = "JSON|*.json", FileName = "observation.json" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            await File.WriteAllTextAsync(dialog.FileName, JsonSerializer.Serialize(_displayedObservation, ObservationStore.JsonOptions), _lifetime.Token);
    }

    private void BuildMinionCommand()
    {
        var page = CreatePage("Minion Command");
        var layout = Theme.Table((SizeType.Absolute, 48), (SizeType.Absolute, 46),
            (SizeType.Absolute, 46), (SizeType.Percent, 100), (SizeType.Absolute, 46));
        page.Controls.Add(layout);
        layout.Controls.Add(_minionStatus, 0, 0);
        layout.Controls.Add(TwoButtons(MakeButton("Import account JSON", ImportMinionsAsync),
            MakeButton("Edit / save account", EditMinionAccountAsync)), 0, 1);
        foreach (var goal in Enum.GetValues<OptimizationGoal>()) _troopGoal.Items.Add(goal);
        foreach (var mode in Enum.GetValues<BattleMode>()) _troopMode.Items.Add(mode);
        _troopGoal.SelectedItem = OptimizationGoal.Balanced;
        _troopMode.SelectedItem = BattleMode.General;
        var choices = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
        choices.ColumnStyles.Add(new(SizeType.Absolute, 58));
        choices.ColumnStyles.Add(new(SizeType.Percent, 50));
        choices.ColumnStyles.Add(new(SizeType.Absolute, 58));
        choices.ColumnStyles.Add(new(SizeType.Percent, 50));
        choices.Controls.Add(Theme.TextLabel("Goal", 9), 0, 0);
        choices.Controls.Add(_troopGoal, 1, 0);
        choices.Controls.Add(Theme.TextLabel("Mode", 9), 2, 0);
        choices.Controls.Add(_troopMode, 3, 0);
        layout.Controls.Add(choices, 0, 2);
        layout.Controls.Add(TextCard("TROOP COMPARISON • ACCOUNT INPUTS & SUPPLIED BONUSES", _minionResults), 0, 3);
        layout.Controls.Add(TwoButtons(MakeButton("Calculate compositions", OptimizeMinionsAsync, true), MakeButton("Load synthetic example", () =>
        {
            SetMinionAccount(SyntheticMinionFixture.Create());
            return Task.CompletedTask;
        })), 0, 4);
        _minionResults.Text = "Import observed account data or edit an empty account. Leadership and each type's Minion MAX are independent limits.\r\n\r\nThe synthetic example demonstrates the optimizer with invented inputs. Scores are stat proxies; game damage and casualties are not yet modeled. No troops are deployed.";
        _minionResults.WordWrap = true;
        var saved = Path.Combine(DataDirectory, "minions.json");
        try { if (File.Exists(saved)) SetMinionAccount(MinionAccountJson.Load(saved)); }
        catch (Exception ex) { _minionStatus.Text = "Account could not be loaded: " + ex.Message; }
    }

    private Task ImportMinionsAsync()
    {
        using var dialog = new OpenFileDialog { Title = "Import minion account", Filter = "JSON|*.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return Task.CompletedTask;
        var account = MinionAccountJson.Load(dialog.FileName);
        MinionAccountJson.Save(Path.Combine(DataDirectory, "minions.json"), account);
        SetMinionAccount(account);
        return Task.CompletedTask;
    }

    private void SetMinionAccount(MinionAccount account)
    {
        _minionAccount = account;
        _minionStatus.Text = $"{(account.Synthetic ? "SYNTHETIC EXAMPLE" : account.AccountName)} • Leadership {account.Leadership} • {account.Minions.Count} types";
        var text = new StringBuilder().AppendLine(_minionStatus.Text).AppendLine();
        foreach (var minion in account.Minions)
            text.AppendLine($"{minion.Name} • {minion.Form} T{minion.Tier}\r\n  Ready {minion.Available}, deployed {minion.Deployed}, healing {minion.Healing}, MAX {minion.MinionMax}\r\n  ATK {minion.Stats.Attack:N1}, DEF {minion.Stats.Defense:N1}, HP {minion.Stats.Hp:N1}, RUSH {minion.Stats.Rush:N1}\r\n  Source: {minion.Evidence.Source} ({minion.Evidence.Confidence:P0})\r\n");
        text.AppendLine("Choose a goal and mode, then calculate compositions. Only supplied bonuses apply.");
        _minionResults.Text = text.ToString();
    }

    private Task EditMinionAccountAsync()
    {
        using var editor = new Form
        {
            Text = "Minion account JSON • observations and evidence", Size = new(900, 680), MinimumSize = new(640, 480),
            StartPosition = FormStartPosition.CenterParent, BackColor = Theme.Background, ForeColor = Theme.Text
        };
        var text = MakeLog();
        text.ReadOnly = false;
        text.AcceptsTab = true;
        text.ScrollBars = ScrollBars.Both;
        text.Text = JsonSerializer.Serialize(_minionAccount ?? new MinionAccount { AccountName = "My account" }, MinionAccountJson.Options);
        var save = new ModernButton("Validate and save", true) { Dock = DockStyle.Bottom, Height = 46 };
        var errorLabel = Theme.TextLabel("Use observed effective stats; do not apply already included bonuses twice.", 9, color: Theme.Warning);
        errorLabel.Dock = DockStyle.Bottom;
        errorLabel.Height = 46;
        save.Click += (_, _) =>
        {
            try
            {
                var account = MinionAccountJson.Parse(text.Text);
                MinionAccountJson.Save(Path.Combine(DataDirectory, "minions.json"), account);
                SetMinionAccount(account);
                editor.DialogResult = DialogResult.OK;
            }
            catch (Exception ex) { errorLabel.Text = ex.Message; }
        };
        editor.Controls.Add(text);
        editor.Controls.Add(errorLabel);
        editor.Controls.Add(save);
        editor.ShowDialog(this);
        return Task.CompletedTask;
    }

    private async Task OptimizeMinionsAsync()
    {
        if (_minionAccount is null) throw new InvalidOperationException("Import an account or load the synthetic example first.");
        var account = _minionAccount;
        var goal = (OptimizationGoal)_troopGoal.SelectedItem!;
        var mode = (BattleMode)_troopMode.SelectedItem!;
        _minionStatus.Text = "Calculating bounded compositions...";
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await Task.Run(() => TroopOptimizer.Optimize(account, new() { Mode = mode }, goal, cancellationToken: _lifetime.Token), _lifetime.Token);
        var text = new StringBuilder().AppendLine($"{goal} • {mode} • {result.Evaluations:N0} evaluations in {stopwatch.ElapsedMilliseconds:N0} ms");
        foreach (var warning in result.Warnings) text.AppendLine(warning);
        int rank = 0;
        foreach (var candidate in result.Candidates)
        {
            text.AppendLine().AppendLine($"#{++rank} • score {candidate.Score:N2} • Leadership {candidate.LeadershipUsed}/{account.Leadership}");
            foreach (var count in candidate.Counts.Where(c => c.Value > 0)) text.AppendLine($"  {account.Minions.First(m => m.Id == count.Key).Name}: {count.Value}");
            text.AppendLine($"  ATK {candidate.Stats.Attack:N1} • DEF {candidate.Stats.Defense:N1} • HP {candidate.Stats.Hp:N1} • RUSH {candidate.Stats.Rush:N1}");
            text.AppendLine($"  Elemental damage {candidate.Stats.ElementalDamage:N1} • resistance {candidate.Stats.ElementalResistance:N1}");
            foreach (var bonus in candidate.ActiveBonuses) text.AppendLine("  Bonus: " + bonus);
            text.AppendLine(candidate.Explanation);
        }
        _minionResults.Text = text.ToString();
        _minionStatus.Text = $"{(account.Synthetic ? "SYNTHETIC" : account.AccountName)} • {result.Candidates.Count} candidates • {stopwatch.ElapsedMilliseconds:N0} ms";
        Log($"Troop analysis: {goal}/{mode}, {result.Evaluations:N0} evaluations, {result.Candidates.Count} candidates. No game action executed.");
    }

    private void RestoreWindowBounds()
    {
        var area = Screen.FromPoint(new Point(_preferences.WindowX, _preferences.WindowY)).WorkingArea;
        Size = new Size(Math.Clamp(_preferences.WindowWidth, MinimumSize.Width, Math.Max(MinimumSize.Width, area.Width)),
            Math.Clamp(_preferences.WindowHeight, MinimumSize.Height, Math.Max(MinimumSize.Height, area.Height)));
        var bounds = new Rectangle(_preferences.WindowX, _preferences.WindowY, Width, Height);
        if (_preferences.WindowX != int.MinValue && Screen.AllScreens.Any(screen => Rectangle.Intersect(screen.WorkingArea, bounds) is { Width: >= 200, Height: >= 100 }))
        {
            StartPosition = FormStartPosition.Manual;
            Location = new Point(Math.Clamp(bounds.X, area.Left, Math.Max(area.Left, area.Right - Width)),
                Math.Clamp(bounds.Y, area.Top, Math.Max(area.Top, area.Bottom - Height)));
        }
    }

    private void SaveWindowBounds()
    {
        var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        _preferences.WindowX = bounds.X; _preferences.WindowY = bounds.Y;
        _preferences.WindowWidth = bounds.Width; _preferences.WindowHeight = bounds.Height;
        SavePreferences();
    }
}
