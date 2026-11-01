using System.Text.Json;
using System.Text.Json.Serialization;

namespace SnailCommander.Core.Minions;

public enum MinionForm { Unknown, Zombie, Demon, Angel, Mutant, Mecha, Dragon, Other }
public enum BattleMode { General, MinionSimulation, SpeciesWar, Domain, Rocket, Gathering }
public enum OptimizationGoal { Balanced, Damage, Survivability, MinimumLossExposure, Speed, ElementalDamage, ElementalDefense }
public enum EvidenceKind { Unverified, Observation, Documented, Synthetic }

public sealed record RuleEvidence
{
    public EvidenceKind Kind { get; init; }
    public string Source { get; init; } = "Unverified";
    public double Confidence { get; init; }
    public DateTimeOffset? VerifiedAt { get; init; }
}

/// <summary>Effective per-minion values supplied by the account; no game damage formula is assumed.</summary>
public sealed record TroopStats
{
    public double Hp { get; init; }
    public double Attack { get; init; }
    public double Defense { get; init; }
    public double Rush { get; init; }
    public double ElementalDamage { get; init; }
    public double ElementalResistance { get; init; }

    public static TroopStats operator +(TroopStats a, TroopStats b) => new()
    {
        Hp = a.Hp + b.Hp, Attack = a.Attack + b.Attack, Defense = a.Defense + b.Defense,
        Rush = a.Rush + b.Rush, ElementalDamage = a.ElementalDamage + b.ElementalDamage,
        ElementalResistance = a.ElementalResistance + b.ElementalResistance
    };
    public static TroopStats operator *(TroopStats a, double scale) => new()
    {
        Hp = a.Hp * scale, Attack = a.Attack * scale, Defense = a.Defense * scale,
        Rush = a.Rush * scale, ElementalDamage = a.ElementalDamage * scale,
        ElementalResistance = a.ElementalResistance * scale
    };
    internal double[] Values => [Hp, Attack, Defense, Rush, ElementalDamage, ElementalResistance];
    internal TroopStats ApplyPercent(TroopStats p) => new()
    {
        Hp = Hp * (1 + p.Hp), Attack = Attack * (1 + p.Attack), Defense = Defense * (1 + p.Defense),
        Rush = Rush * (1 + p.Rush), ElementalDamage = ElementalDamage * (1 + p.ElementalDamage),
        ElementalResistance = ElementalResistance * (1 + p.ElementalResistance)
    };
}

public sealed record MinionType
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Partner { get; init; } = "";
    public MinionForm Form { get; init; }
    public int Tier { get; init; }
    public TroopStats? BaseStats { get; init; }
    public TroopStats Stats { get; init; } = new();
    /// <summary>Ready and undeployed copies only. Healing and deployed copies cannot fight again.</summary>
    public int Available { get; init; }
    public int MinionMax { get; init; }
    public int Deployed { get; init; }
    public int Healing { get; init; }
    public Dictionary<string, double> CloneCost { get; init; } = new();
    public double CloneMinutes { get; init; }
    public string AbilityNotes { get; init; } = "";
    public RuleEvidence Evidence { get; init; } = new();
    [JsonIgnore] public int Owned => Available + Deployed + Healing;
}

/// <summary>
/// Explicit additive percentage and flat bonuses. RequiredCount/PerGroup encode supplied
/// thresholds (including 10/60); these are not asserted as universal Super Snail mechanics.
/// Percent values use fractions, e.g. 0.1 means +10%. Mode null means all modes.
/// Do not include effects already incorporated into MinionType.Stats.
/// </summary>
public sealed record TroopBonus
{
    public string Name { get; init; } = "";
    public BattleMode? Mode { get; init; }
    public MinionForm? TriggerForm { get; init; }
    public MinionForm? TargetForm { get; init; }
    public int RequiredCount { get; init; } = 1;
    public bool PerGroup { get; init; }
    public int MaxStacks { get; init; } = 1;
    public TroopStats Percent { get; init; } = new();
    public TroopStats Flat { get; init; } = new();
    public RuleEvidence Evidence { get; init; } = new();
}

public sealed record MinionAccount
{
    public string AccountName { get; init; } = "";
    public bool Synthetic { get; init; }
    public int Leadership { get; init; }
    public List<MinionType> Minions { get; init; } = new();
    public List<TroopBonus> Bonuses { get; init; } = new();
    [JsonIgnore] public bool ContainsSyntheticData => Synthetic
        || Minions.Any(m => m.Evidence.Kind == EvidenceKind.Synthetic)
        || Bonuses.Any(b => b.Evidence.Kind == EvidenceKind.Synthetic);

    public void Validate()
    {
        if (Leadership is < 0 or > 100000) throw new ArgumentException("Leadership must be between 0 and 100000.");
        if (Minions is null || Bonuses is null || Minions.Count > 200 || Bonuses.Count > 500)
            throw new ArgumentException("Provide at most 200 minion types and 500 bonuses.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var m in Minions)
        {
            if (m is null || string.IsNullOrWhiteSpace(m.Id) || !ids.Add(m.Id) || string.IsNullOrWhiteSpace(m.Name))
                throw new ArgumentException("Minion IDs must be nonempty and unique; each minion needs a name.");
            if (!Enum.IsDefined(m.Form) || m.Tier < 0 || m.MinionMax is < 0 or > 100000 || m.Available < 0 || m.Deployed < 0 || m.Healing < 0
                || (long)m.Available + m.Deployed + m.Healing > m.MinionMax)
                throw new ArgumentException($"Invalid quantity, cap, form, or tier for {m.Name}. Available + deployed + healing must not exceed Minion MAX.");
            ValidateStats(m.Stats, m.Name);
            if (m.BaseStats is not null) ValidateStats(m.BaseStats, m.Name);
            ValidateEvidence(m.Evidence);
            if (!double.IsFinite(m.CloneMinutes) || m.CloneMinutes < 0 || m.CloneCost is null
                || m.CloneCost.Any(x => string.IsNullOrWhiteSpace(x.Key) || !double.IsFinite(x.Value) || x.Value < 0))
                throw new ArgumentException($"Invalid cloning costs for {m.Name}.");
        }
        foreach (var b in Bonuses)
        {
            if (b is null || string.IsNullOrWhiteSpace(b.Name) || b.RequiredCount is < 1 or > 100000 || b.MaxStacks is < 1 or > 100000
                || (b.Mode.HasValue && !Enum.IsDefined(b.Mode.Value)) || (b.TriggerForm.HasValue && !Enum.IsDefined(b.TriggerForm.Value))
                || (b.TargetForm.HasValue && !Enum.IsDefined(b.TargetForm.Value)))
                throw new ArgumentException("Invalid bonus threshold, stack limit, or classification.");
            ValidateStats(b.Percent, b.Name); ValidateStats(b.Flat, b.Name); ValidateEvidence(b.Evidence);
        }
    }

    private static void ValidateStats(TroopStats? stats, string name)
    {
        if (stats is null || stats.Values.Any(x => !double.IsFinite(x) || x < 0 || x > 1e12))
            throw new ArgumentException($"{name}: statistics must be finite, nonnegative, and at most 1e12.");
    }
    private static void ValidateEvidence(RuleEvidence? evidence)
    {
        if (evidence is null || !Enum.IsDefined(evidence.Kind) || string.IsNullOrWhiteSpace(evidence.Source)
            || !double.IsFinite(evidence.Confidence) || evidence.Confidence is < 0 or > 1)
            throw new ArgumentException("Every rule requires a source and confidence between 0 and 1.");
    }
}

public static class MinionAccountJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true, PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
        MaxDepth = 32
    };
    public static MinionAccount Parse(string json)
    {
        if (json.Length > 2 * 1024 * 1024) throw new ArgumentException("Account JSON exceeds 2 MB.");
        var account = JsonSerializer.Deserialize<MinionAccount>(json, Options) ?? throw new ArgumentException("Missing account.");
        account.Validate();
        // Editing the top-level flag cannot turn synthetic source data into a real account.
        return account with { Synthetic = account.ContainsSyntheticData };
    }
    public static MinionAccount Load(string path) => Parse(File.ReadAllText(path));
    public static void Save(string path, MinionAccount account)
    {
        account.Validate();
        account = account with { Synthetic = account.ContainsSyntheticData };
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(account, Options)); File.Move(temp, full, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
