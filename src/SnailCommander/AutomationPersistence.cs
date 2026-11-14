using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SnailCommander.Core.Automation;

namespace SnailCommander;

/// <summary>Per-profile single writer, durable retry reservations, and latest run report. No automatic input replay.</summary>
internal sealed class AutomationPersistence : IDisposable
{
    private readonly string _folder, _budgetPath;
    private readonly FileStream _lease;
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private readonly string _runId = Guid.NewGuid().ToString("N");
    public RecoveryBudget Budget { get; }
    public AutomationPersistence(string folder, string device)
    {
        _folder = folder;
        Directory.CreateDirectory(folder);
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(device)))[..24];
        _budgetPath = Path.Combine(folder, "recovery-" + key + ".json");
        _lease = new FileStream(Path.Combine(folder, "session.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            if (File.Exists(_budgetPath) && new FileInfo(_budgetPath).Length > 16384) throw new InvalidDataException("Recovery ledger exceeds its size limit.");
            Budget = File.Exists(_budgetPath) ? JsonSerializer.Deserialize<RecoveryBudget>(File.ReadAllText(_budgetPath))
                ?? throw new InvalidDataException("Recovery ledger is empty.") : new RecoveryBudget();
            if (Budget.Attempts is null) throw new InvalidDataException("Recovery ledger is invalid; it was not reset.");
            WriteRun(null);
        }
        catch { _lease.Dispose(); throw; }
    }
    public void SaveBudget() => AtomicWrite(_budgetPath, JsonSerializer.Serialize(Budget));
    public void WriteRun(AutomationRunResult? result) => AtomicWrite(Path.Combine(_folder, "last-run.json"),
        JsonSerializer.Serialize(new { runId = _runId, startedAt = _startedAt, finishedAt = result is null ? (DateTimeOffset?)null : DateTimeOffset.UtcNow, result }));
    private static void AtomicWrite(string path, string text)
    {
        File.WriteAllText(path + ".tmp", text);
        File.Move(path + ".tmp", path, true);
    }
    public void Dispose() => _lease.Dispose();
}
