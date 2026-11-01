using System.Diagnostics;
using System.Text.Json;
using SnailCommander.Core.Minions;

public static class MinionTests
{
    public static void Run(Action<bool, string> assert)
    {
        var context = new BattleContext();
        var small = new MinionAccount
        {
            Leadership = 4,
            Minions =
            [
                Unit("a", 4, new() { Attack = 10, Hp = 10 }),
                Unit("b", 4, new() { Attack = 7, Hp = 100 }, MinionForm.Angel)
            ],
            Bonuses = [new() { Name = "Synthetic mixed threshold", TriggerForm = MinionForm.Angel,
                RequiredCount = 2, Percent = new() { Attack = 1 }, Evidence = Evidence() }]
        };
        var beamTime = Stopwatch.StartNew();
        var optimized = TroopOptimizer.Optimize(small, context, OptimizationGoal.Damage,
            new() { BeamWidth = 1000, MaxEvaluations = 10000 });
        beamTime.Stop();
        var exhaustiveTime = Stopwatch.StartNew();
        double bruteBest = double.NegativeInfinity;
        for (int a = 0; a <= 4; a++)
            for (int b = 0; b <= 4 - a; b++)
                bruteBest = Math.Max(bruteBest, TroopOptimizer.Evaluate(small, context, OptimizationGoal.Damage,
                    new Dictionary<string, int> { ["a"] = a, ["b"] = b }).Score);
        exhaustiveTime.Stop();
        Console.WriteLine($"Minion benchmark (tiny synthetic, includes validation): beam {beamTime.Elapsed.TotalMilliseconds:F2} ms / {optimized.Evaluations} evaluations; exhaustive {exhaustiveTime.Elapsed.TotalMilliseconds:F2} ms / 15 compositions. Timing is illustrative, not a performance guarantee.");
        assert(Math.Abs(optimized.Best.Score - bruteBest) < 1e-8, "Beam matches exhaustive small-account optimum");
        assert(optimized.Best.Counts.GetValueOrDefault("a") == 2 && optimized.Best.Counts.GetValueOrDefault("b") == 2,
            "Lower standalone strength can win through supplied composition synergy");
        assert(!optimized.SearchLimited, "Unpruned complete search reports no cutoff");

        var ordinary = small with { Bonuses = [] };
        var damage = TroopOptimizer.Optimize(ordinary, context, OptimizationGoal.Damage).Best;
        var defense = TroopOptimizer.Optimize(ordinary, context, OptimizationGoal.Survivability).Best;
        assert(damage.Counts.GetValueOrDefault("a") == 4, "Damage goal prefers attack stats");
        assert(defense.Counts.GetValueOrDefault("b") == 4, "Survivability goal prefers defense/hp stats");

        var capped = new MinionAccount { Leadership = 10, Minions =
            [Unit("a", 2, new() { Attack = 100 }) with { MinionMax = 9, Deployed = 3, Healing = 4 }] };
        var cappedBest = TroopOptimizer.Optimize(capped, context).Best;
        assert(cappedBest.LeadershipUsed == 2, "Deployed and healing minions cannot be redeployed");
        assert(cappedBest.Counts["a"] == 2, "Leadership does not override available quantity or Minion MAX");
        assert(Throws(() => TroopOptimizer.Evaluate(capped, context, OptimizationGoal.Damage,
            new Dictionary<string, int> { ["a"] = 3 })), "Explicit composition rejects unavailable minions");
        assert(Throws(() => TroopOptimizer.Evaluate(small, context, OptimizationGoal.Damage,
            new Dictionary<string, int> { ["a"] = 4, ["b"] = 1 })), "Explicit composition rejects Leadership overflow");
        assert(Throws(() => TroopOptimizer.Evaluate(small, context, OptimizationGoal.Damage,
            new Dictionary<string, int> { ["a"] = -1 })), "Negative deployment rejected");
        assert(Throws(() => TroopOptimizer.Evaluate(small, context, OptimizationGoal.Damage,
            new Dictionary<string, int> { ["unknown"] = 1 })), "Unknown minion ID rejected");

        var thresholds = new MinionAccount
        {
            Leadership = 60, Minions = [Unit("a", 60, new() { Attack = 10 }, MinionForm.Demon)],
            Bonuses =
            [
                new() { Name = "Every ten synthetic", RequiredCount = 10, PerGroup = true, MaxStacks = 6,
                    Percent = new() { Attack = .1 }, Evidence = Evidence() },
                new() { Name = "Sixty synthetic", RequiredCount = 60, Percent = new() { Attack = .4 }, Evidence = Evidence() }
            ]
        };
        var nine = TroopOptimizer.Evaluate(thresholds, context, OptimizationGoal.Damage, new Dictionary<string, int> { ["a"] = 9 });
        var twenty = TroopOptimizer.Evaluate(thresholds, context, OptimizationGoal.Damage, new Dictionary<string, int> { ["a"] = 20 });
        var sixty = TroopOptimizer.Evaluate(thresholds, context, OptimizationGoal.Damage, new Dictionary<string, int> { ["a"] = 60 });
        assert(nine.ActiveBonuses.Count == 0 && Math.Abs(nine.Stats.Attack - 90) < 1e-8, "Threshold bonuses do not activate early");
        assert(Math.Abs(twenty.Stats.Attack - 240) < 1e-8, "Per-ten bonus stacks correctly");
        assert(Math.Abs(sixty.Stats.Attack - 1200) < 1e-8 && sixty.ActiveBonuses.Count == 2, "Per-ten and sixty thresholds combine additively");

        var modes = thresholds with { Bonuses = [new() { Name = "Synthetic sim only", Mode = BattleMode.MinionSimulation,
            RequiredCount = 1, TargetForm = MinionForm.Demon, Percent = new() { Attack = 1 }, Evidence = Evidence() }] };
        var counts = new Dictionary<string, int> { ["a"] = 1 };
        assert(TroopOptimizer.Evaluate(modes, context, OptimizationGoal.Damage, counts).Stats.Attack == 10,
            "Mode-specific bonuses do not leak into other modes");
        assert(TroopOptimizer.Evaluate(modes, new() { Mode = BattleMode.MinionSimulation }, OptimizationGoal.Damage, counts).Stats.Attack == 20,
            "Mode-specific supplied bonuses apply in matching mode");

        var sampled = TroopOptimizer.Optimize(thresholds, context, OptimizationGoal.Damage,
            new() { BeamWidth = 1, MaxEvaluations = 3, MaxQuantityChoices = 2 });
        assert(sampled.SearchLimited && sampled.QuantitySampled && sampled.Evaluations <= 3,
            "Search honors hard evaluation budget and discloses heuristic truncation");
        assert(sampled.Best.LeadershipUsed <= thresholds.Leadership && sampled.Best.Counts.GetValueOrDefault("a") <= 60,
            "Truncated search still returns a legal composition");
        var lateStrong = new MinionAccount { Leadership = 1000, Minions =
            [Unit("first", 1000, new() { Attack = 1 }), Unit("last", 1000, new() { Attack = 100 })] };
        var boundedLarge = TroopOptimizer.Optimize(lateStrong, context, OptimizationGoal.Damage,
            new() { BeamWidth = 2, MaxEvaluations = 4, MaxQuantityChoices = 2 });
        assert(boundedLarge.Best.Counts.GetValueOrDefault("last") == 1000 && boundedLarge.Evaluations <= 4,
            "Complete baseline prevents early quantity expansion starving a stronger later type");
        var reservedThreshold = new MinionAccount
        {
            Leadership = 60, Minions = [Unit("strong", 60, new() { Attack = 10 }),
                Unit("support", 60, new() { Attack = 1 }, MinionForm.Angel)],
            Bonuses = [new() { Name = "Synthetic later support threshold", TriggerForm = MinionForm.Angel,
                RequiredCount = 10, Percent = new() { Attack = 4 }, Evidence = Evidence() }]
        };
        var reserved = TroopOptimizer.Optimize(reservedThreshold, context, OptimizationGoal.Damage,
            new() { BeamWidth = 2, MaxQuantityChoices = 2, MaxEvaluations = 1000 }).Best;
        assert(reserved.Counts.GetValueOrDefault("strong") == 50 && reserved.Counts.GetValueOrDefault("support") == 10,
            "Quantity sampling reserves room for a later minion type's synergy threshold");
        var empty = TroopOptimizer.Optimize(new(), context);
        assert(empty.Best.LeadershipUsed == 0 && empty.Best.Score == 0, "Empty real account produces honest empty composition");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        assert(Throws<OperationCanceledException>(() => TroopOptimizer.Optimize(small, context, cancellationToken: cancelled.Token)),
            "Optimization respects cancellation");

        assert(Throws(() => (small with { Minions = [small.Minions[0], small.Minions[0]] }).Validate()), "Duplicate IDs rejected");
        assert(Throws(() => (small with { Minions = [small.Minions[0] with { Stats = new() { Hp = -1 } }] }).Validate()),
            "Negative effective stats rejected");
        assert(Throws(() => (capped with { Minions = [capped.Minions[0] with { MinionMax = 8 }] }).Validate()),
            "Ready plus deployed plus healing cannot exceed Minion MAX");
        assert(Throws(() => (small with { Bonuses = [new() { Name = "bad", RequiredCount = 0 }] }).Validate()),
            "Zero threshold rejected");
        assert(Throws(() => (small with { Minions = [small.Minions[0] with { Evidence = new() { Source = "test", Confidence = 1.1 } }] }).Validate()),
            "Invalid evidence confidence rejected");

        var cloneAccount = new MinionAccount { Leadership = 8, Minions = [Unit("a", 2, new() { Attack = 10 }) with
            { MinionMax = 8, Deployed = 1, Healing = 1, CloneCost = new() { ["material"] = 10 }, CloneMinutes = 3 }] };
        var clonePlan = ClonePlanner.Plan(cloneAccount, new() { Balances = new() { ["material"] = 50 },
            Reserves = new() { ["material"] = 20 } }, new Dictionary<string, int> { ["a"] = 8 });
        assert(clonePlan.Queue.Sum(x => x.Quantity) == 3 && clonePlan.RemainingBalances["material"] == 20,
            "Clone planning preserves explicit resource reserves");
        assert(clonePlan.Queue.Sum(x => x.Minutes) == 9, "Clone queue includes elapsed cloning workload");
        var capPlan = ClonePlanner.Plan(cloneAccount, new() { Balances = new() { ["material"] = 1000 } },
            new Dictionary<string, int> { ["a"] = 8 });
        assert(capPlan.Queue.Sum(x => x.Quantity) == 4, "Clone planning respects cap occupied by healing/deployed copies");
        var unknownCost = ClonePlanner.Plan(cloneAccount with { Minions = [cloneAccount.Minions[0] with { CloneCost = new() }] },
            new(), new Dictionary<string, int> { ["a"] = 8 });
        assert(unknownCost.Queue.Count == 0 && unknownCost.Blockers.Any(x => x.Contains("unknown")), "Unknown cloning costs are never treated as free");
        assert(Throws(() => ClonePlanner.Plan(cloneAccount, new(), new Dictionary<string, int> { ["missing"] = 1 })),
            "Cloning rejects unknown demand IDs");
        assert(Throws(() => ClonePlanner.Plan(cloneAccount, new() { Balances = new() { ["material"] = -1 } },
            new Dictionary<string, int> { ["a"] = 3 })), "Negative resource balance is rejected");
        assert(Throws(() => ClonePlanner.Plan(cloneAccount, new() { Reserves = new() { ["material"] = -1 } },
            new Dictionary<string, int> { ["a"] = 3 })), "Negative reserve is rejected");
        var excessiveReserve = ClonePlanner.Plan(cloneAccount, new() { Balances = new() { ["material"] = 10 },
            Reserves = new() { ["material"] = 20 } }, new Dictionary<string, int> { ["a"] = 3 });
        assert(excessiveReserve.Queue.Count == 0 && excessiveReserve.RemainingBalances["material"] == 10,
            "Reserves above balance prevent spending without changing balances");
        var boundedClone = ClonePlanner.Plan(cloneAccount, new() { Balances = new() { ["material"] = 1000 } },
            new Dictionary<string, int> { ["a"] = 8 }, maxCopies: 1);
        assert(boundedClone.Queue.Sum(x => x.Quantity) == 1 && boundedClone.Blockers.Any(x => x.Contains("copy limit")),
            "Cloning honors copy budget and discloses remaining demand");

        var fixture = SyntheticMinionFixture.Create();
        var json = JsonSerializer.Serialize(fixture, MinionAccountJson.Options);
        var roundtrip = MinionAccountJson.Parse(json);
        assert(roundtrip.Synthetic && roundtrip.Minions.Count == 3 && roundtrip.Minions[0].Form == MinionForm.Demon,
            "Account JSON roundtrip preserves synthetic flag, minions, and form enum");
        assert(TroopOptimizer.Optimize(roundtrip, context).Warnings.Any(x => x.Contains("SYNTHETIC")), "Synthetic result is prominently disclosed");
        var removedFlag = fixture with { Synthetic = false };
        assert(MinionAccountJson.Parse(JsonSerializer.Serialize(removedFlag, MinionAccountJson.Options)).Synthetic,
            "Import restores synthetic flag from nested evidence");
        assert(TroopOptimizer.Optimize(removedFlag, context).Warnings.Any(x => x.Contains("SYNTHETIC")),
            "In-memory synthetic provenance is disclosed even if account flag is cleared");
        var temp = Path.Combine(AppContext.BaseDirectory, "snail-minion-tests-" + Guid.NewGuid().ToString("N"), "account.json");
        try
        {
            MinionAccountJson.Save(temp, fixture);
            assert(MinionAccountJson.Load(temp).Leadership == 30, "Account persistence loads saved Leadership");
            MinionAccountJson.Save(temp, fixture with { Leadership = 12 });
            assert(MinionAccountJson.Load(temp).Leadership == 12, "Atomic account replacement persists new value");
            MinionAccountJson.Save(temp, removedFlag);
            assert(MinionAccountJson.Load(temp).Synthetic, "Saving cannot strip nested synthetic provenance");
        }
        finally { if (File.Exists(temp)) File.Delete(temp); if (Directory.Exists(Path.GetDirectoryName(temp))) Directory.Delete(Path.GetDirectoryName(temp)!); }
    }

    private static MinionType Unit(string id, int available, TroopStats stats, MinionForm form = MinionForm.Demon)
        => new() { Id = id, Name = "Synthetic " + id, Available = available, MinionMax = available, Stats = stats, Form = form, Evidence = Evidence() };
    private static RuleEvidence Evidence() => new() { Kind = EvidenceKind.Synthetic, Source = "Test-only invented data", Confidence = 1 };
    private static bool Throws(Action action) => Throws<ArgumentException>(action);
    private static bool Throws<T>(Action action) where T : Exception { try { action(); return false; } catch (T) { return true; } }
}
