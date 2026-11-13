using System.Text;
using System.Text.Json;
using SnailCommander.Core.Accounts;
using SnailCommander.Core.Observation;

namespace SnailCommander;

public sealed partial class MainForm
{
    private readonly DataGridView _accountFields = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false, RowHeadersVisible = false, AutoGenerateColumns = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
        BackgroundColor = Theme.Card, BorderStyle = BorderStyle.None, EnableHeadersVisualStyles = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
    };
    private readonly Label _accountStatus = Theme.TextLabel("Waiting for live labeled readings", 11, true, Theme.Accent);
    private readonly TextBox _accountDetails = MakeLog();
    private readonly SemaphoreSlim _accountGate = new(1, 1);
    private readonly System.Windows.Forms.Timer _accountAgeTimer = new() { Interval = 15000 };
    private IReadOnlyList<ScreenObservation> _accountEvidenceFrames = [];
    private AccountEvidenceSnapshot? _accountSnapshot;

    private void BuildAccountEvidence()
    {
        var page = CreatePage("Account");
        var layout = Theme.Table((SizeType.Absolute, 48), (SizeType.Absolute, 42),
            (SizeType.Percent, 60), (SizeType.Percent, 40), (SizeType.Absolute, 46));
        page.Controls.Add(layout);
        layout.Controls.Add(_accountStatus, 0, 0);
        layout.Controls.Add(Theme.TextLabel("Live OCR evidence • 10-minute freshness • Repeated = matching frames • Missing values stay unknown", 9, color: Theme.Secondary), 0, 1);
        _accountFields.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.Card, ForeColor = Theme.Text, SelectionBackColor = Theme.AccentDark,
            SelectionForeColor = Theme.Text, Font = new Font("Segoe UI", 9)
        };
        _accountFields.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Theme.Raised, ForeColor = Theme.Text };
        foreach (var (name, width) in new[] { ("Field", 85f), ("Minion / context", 145f), ("Value", 70f), ("State", 85f), ("Last seen", 75f) })
            _accountFields.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = name, FillWeight = width, SortMode = DataGridViewColumnSortMode.NotSortable });
        _accountFields.SelectionChanged += (_, _) => ShowSelectedAccountEvidence();
        layout.Controls.Add(_accountFields, 0, 2);
        _accountDetails.WordWrap = true;
        layout.Controls.Add(TextCard("SOURCE EVIDENCE", _accountDetails), 0, 3);
        layout.Controls.Add(TwoButtons(MakeButton("Refresh live evidence", () => RefreshAccountEvidenceAsync()),
            MakeButton("Export evidence JSON", ExportAccountEvidenceAsync)), 0, 4);
        _accountAgeTimer.Tick += async (_, _) =>
        {
            if (_pages["Account"].Visible) await RefreshAccountEvidenceAsync(reload: false);
        };
        Shown += (_, _) => _accountAgeTimer.Start();
    }

    private void InvalidateAccountEvidence()
    {
        _accountSnapshot = null;
        _accountFields.Rows.Clear();
        _accountDetails.Clear();
        _accountStatus.Text = "Waiting for current-device evidence";
    }

    private async Task RefreshAccountEvidenceAsync(bool reload = true)
    {
        if (IsDisposed || Disposing || _lifetime.IsCancellationRequested || !_accountGate.Wait(0)) return;
        string device = _selectedDevice;
        try
        {
            string[] names = _minionAccount is not null && !_minionAccount.ContainsSyntheticData
                ? _minionAccount.Minions.Select(m => m.Name).ToArray() : [];
            var projected = await Task.Run(() =>
            {
                var frames = reload || _accountEvidenceFrames.Count == 0 ? Observer.Store.LoadRecent(200) : _accountEvidenceFrames;
                return (Frames: frames, Snapshot: AccountEvidenceProjection.Build(frames, device, DateTimeOffset.UtcNow, names));
            }, _lifetime.Token);
            if (IsDisposed || Disposing || _lifetime.IsCancellationRequested || device != _selectedDevice) return;
            _accountEvidenceFrames = projected.Frames;
            _accountSnapshot = projected.Snapshot;
            _accountFields.Rows.Clear();
            foreach (var field in projected.Snapshot.Fields)
            {
                int row = _accountFields.Rows.Add(FieldName(field.Field), field.MinionName ?? field.Scope,
                    field.Value is null ? "Unknown" : field.Evidence.Any(e => e.CapturedAt == field.LastSeen && e.Rounded) ? "~" + field.Value : field.Value,
                    field.State, field.LastSeen.LocalDateTime.ToString("HH:mm:ss"));
                _accountFields.Rows[row].Tag = field;
                if (field.State is ReadingState.Stale or ReadingState.Conflicting or ReadingState.Unassigned)
                    _accountFields.Rows[row].DefaultCellStyle.ForeColor = Theme.Warning;
            }
            _accountStatus.Text = string.IsNullOrWhiteSpace(device) ? "Select a connected emulator to view live evidence"
                : $"{device} • {projected.Snapshot.Fields.Count} labeled fields • updated {DateTime.Now:HH:mm:ss}";
            if (projected.Snapshot.Fields.Count == 0)
                _accountDetails.Text = "No supported live labeled fields have been established for this device. Visit a minion detail screen and allow observation to run. Imported images, replays, old records without provenance and synthetic fixtures are excluded from live account state.\r\n\r\nThis view does not fill or overwrite the optimizer's inventory. Account identity, current balance and effective combat stats still require verification.";
            else ShowSelectedAccountEvidence();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing) _accountStatus.Text = "Account evidence unavailable: " + ex.Message;
        }
        finally { _accountGate.Release(); }
    }

    private static string FieldName(AccountField field) => field switch
    {
        AccountField.MinionMax => "Minion MAX", AccountField.BTadsReading => "B-Tads reading",
        AccountField.WTadsReading => "W-Tads reading", AccountField.Hp => "HP",
        AccountField.Attack => "ATK", AccountField.Defense => "DEF", AccountField.Rush => "RUSH", _ => field.ToString()
    };

    private void ShowSelectedAccountEvidence()
    {
        if (_accountFields.CurrentRow?.Tag is not AccountFieldEvidence field) return;
        var text = new StringBuilder().AppendLine($"{FieldName(field.Field)} • {field.MinionName ?? "No minion identity"}")
            .AppendLine($"Context: {field.Scope}")
            .AppendLine($"State: {field.State} • matching frames: {field.MatchingFrames}")
            .AppendLine("OCR evidence is not a verified balance or authorization to spend/deploy.")
            .AppendLine("Device identity does not establish account identity.");
        foreach (var item in field.Evidence)
            text.AppendLine().AppendLine($"{item.CapturedAt.LocalDateTime:yyyy-MM-dd HH:mm:ss} • {item.Origin} • {item.Device}")
                .AppendLine($"Observation: {item.ObservationId}").AppendLine($"Text: {item.RawText}");
        _accountDetails.Text = text.ToString();
    }

    private async Task ExportAccountEvidenceAsync()
    {
        if (_accountSnapshot is null) await RefreshAccountEvidenceAsync();
        if (_accountSnapshot is null) throw new InvalidOperationException("No evidence snapshot is available.");
        string device = _selectedDevice;
        var frames = _accountEvidenceFrames;
        string[] names = _minionAccount is not null && !_minionAccount.ContainsSyntheticData
            ? _minionAccount.Minions.Select(m => m.Name).ToArray() : [];
        var snapshot = await Task.Run(() => AccountEvidenceProjection.Build(frames, device, DateTimeOffset.UtcNow, names), _lifetime.Token);
        using var dialog = new SaveFileDialog { Filter = "JSON|*.json", FileName = "account-evidence.json" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            await File.WriteAllTextAsync(dialog.FileName, JsonSerializer.Serialize(snapshot, ObservationStore.JsonOptions), _lifetime.Token);
    }
}
