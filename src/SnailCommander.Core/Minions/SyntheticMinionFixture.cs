namespace SnailCommander.Core.Minions;

/// <summary>Invented mechanics and values for demonstration/testing only.</summary>
public static class SyntheticMinionFixture
{
    public static MinionAccount Create() => new()
    {
        AccountName = "Synthetic example — not your account", Synthetic = true, Leadership = 30,
        Minions =
        [
            new() { Id = "example-a", Name = "Example attacker", Form = MinionForm.Demon, Tier = 1,
                Available = 25, MinionMax = 30, Deployed = 2, Healing = 1,
                Stats = new() { Hp = 100, Attack = 32, Defense = 5, Rush = 4, ElementalDamage = 6 },
                CloneCost = new() { ["example-material"] = 10 }, CloneMinutes = 4, Evidence = Evidence() },
            new() { Id = "example-b", Name = "Example defender", Form = MinionForm.Angel, Tier = 1,
                Available = 20, MinionMax = 25, Healing = 2,
                Stats = new() { Hp = 350, Attack = 18, Defense = 24, Rush = 3, ElementalResistance = 10 },
                CloneCost = new() { ["example-material"] = 8 }, CloneMinutes = 3, Evidence = Evidence() },
            new() { Id = "example-c", Name = "Example swift", Form = MinionForm.Mecha, Tier = 1,
                Available = 10, MinionMax = 15,
                Stats = new() { Hp = 120, Attack = 25, Defense = 10, Rush = 30, ElementalDamage = 2 },
                CloneCost = new() { ["example-material"] = 12 }, CloneMinutes = 6, Evidence = Evidence() }
        ],
        Bonuses =
        [
            new() { Name = "Invented defender group synergy", TriggerForm = MinionForm.Angel, RequiredCount = 10,
                PerGroup = true, MaxStacks = 2, Percent = new() { Attack = .15 }, Evidence = Evidence() },
            new() { Name = "Invented simulation attack bonus", Mode = BattleMode.MinionSimulation,
                TriggerForm = MinionForm.Mecha, TargetForm = MinionForm.Mecha, RequiredCount = 5,
                Percent = new() { Attack = .4 }, Evidence = Evidence() }
        ]
    };

    private static RuleEvidence Evidence() => new()
    {
        Kind = EvidenceKind.Synthetic, Source = "Invented fixture; no game-mechanics claim", Confidence = 1
    };
}
