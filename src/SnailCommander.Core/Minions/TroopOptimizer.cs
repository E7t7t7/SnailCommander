namespace SnailCommander.Core.Minions;

public sealed record BattleContext
{
    public BattleMode Mode { get; init; }
    /// <summary>Optional caller-calibrated linear stat weights; absent means the explicit heuristic preset.</summary>
    public TroopStats? ScoreWeights { get; init; }
}

public sealed record OptimizationOptions
{
    public int BeamWidth { get; init; } = 128;
    public int MaxEvaluations { get; init; } = 100000;
    public int MaxQuantityChoices { get; init; } = 256;
    public int ResultCount { get; init; } = 5;
}

public sealed record TroopCandidate(
    IReadOnlyDictionary<string, int> Counts, int LeadershipUsed, TroopStats Stats,
    double Score, IReadOnlyList<string> ActiveBonuses, string Explanation);

public sealed record OptimizationResult(
    IReadOnlyList<TroopCandidate> Candidates, int Evaluations, bool SearchLimited,
    bool QuantitySampled, IReadOnlyList<string> Warnings)
{
    public TroopCandidate Best => Candidates[0];
}

/// <summary>
/// Bounded beam search over legal counts, with supplied thresholds evaluated jointly.
/// Scores are explainable stat proxies, not a validated damage/casualty simulation.
/// No optimality claim is made when the beam prunes or quantities are sampled.
/// </summary>
public static class TroopOptimizer
{
    private sealed record State(int[] Counts, int Used, TroopCandidate Candidate);

    public static OptimizationResult Optimize(MinionAccount account, BattleContext context,
        OptimizationGoal goal = OptimizationGoal.Balanced, OptimizationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        account.Validate(); ValidateContext(context, goal);
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new();
        if (options.BeamWidth is < 1 or > 10000 || options.MaxEvaluations is < 1 or > 1000000
            || options.MaxQuantityChoices is < 2 or > 10000 || options.ResultCount is < 1 or > 100)
            throw new ArgumentException("Invalid search bounds.");
        var zero = new int[account.Minions.Count];
        var beam = new List<State> { new(zero, 0, EvaluateInternal(account, context, goal, zero, describe: false)) };
        int evaluated = 0;
        bool limited = false, sampled = false, stop = false;
        // Establish useful complete baselines before expanding the beam. Otherwise a large
        // early inventory could consume the entire budget before later types are considered.
        var baselines = new List<State>();
        var weights = context.ScoreWeights ?? WeightsFor(goal);
        var orderedTypes = Enumerable.Range(0, account.Minions.Count)
            .OrderByDescending(i => Weighted(account.Minions[i].Stats, weights)).ToArray();
        if (orderedTypes.Length > 0)
        {
            var greedy = new int[account.Minions.Count];
            int remaining = account.Leadership;
            foreach (int i in orderedTypes)
            {
                greedy[i] = Math.Min(account.Minions[i].Available, remaining);
                remaining -= greedy[i];
            }
            baselines.Add(new(greedy, account.Leadership - remaining, EvaluateInternal(account, context, goal, greedy, describe: false)));
            evaluated++;
            foreach (int i in orderedTypes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (evaluated >= options.MaxEvaluations) { limited = stop = true; break; }
                var single = new int[account.Minions.Count];
                single[i] = Math.Min(account.Minions[i].Available, account.Leadership);
                baselines.Add(new(single, single[i], EvaluateInternal(account, context, goal, single, describe: false)));
                evaluated++;
            }
        }
        for (int index = 0; index < account.Minions.Count && !stop; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var next = new PriorityQueue<State, (double Score, int Used, long Order)>();
            long order = 0;
            foreach (var state in beam)
            {
                int cap = Math.Min(account.Minions[index].Available, account.Leadership - state.Used);
                var quantities = QuantityChoices(account, context, index, cap, state.Counts, options.MaxQuantityChoices, out var reduced);
                sampled |= reduced;
                foreach (int count in quantities)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (evaluated >= options.MaxEvaluations) { limited = stop = true; break; }
                    var counts = (int[])state.Counts.Clone();
                    counts[index] = count;
                    var candidate = EvaluateInternal(account, context, goal, counts, describe: false);
                    var child = new State(counts, state.Used + count, candidate);
                    next.Enqueue(child, (candidate.Score, child.Used, -order++));
                    evaluated++;
                    if (next.Count > options.BeamWidth) { next.Dequeue(); limited = true; }
                }
                if (stop) break;
            }
            // A budget cutoff preserves the best previous complete legal composition as well.
            if (stop)
                foreach (var state in beam)
                {
                    next.Enqueue(state, (state.Candidate.Score, state.Used, -order++));
                    if (next.Count > options.BeamWidth) next.Dequeue();
                }
            if (next.Count > 0)
                beam = next.UnorderedItems.Select(x => x.Element).OrderByDescending(x => x.Candidate.Score)
                    .ThenByDescending(x => x.Used).ThenBy(x => string.Join(",", x.Counts), StringComparer.Ordinal).ToList();
        }
        var warnings = new List<string>
        {
            "Scores use linear stat weights and supplied additive bonuses. Battle damage, duration, and casualties are not predicted.",
            "Only ready, undeployed minions are eligible. Leadership and each type's Minion MAX are separate constraints."
        };
        if (limited || sampled) warnings.Add("Search bounds were reached: this is the best candidate found, not a proven global optimum.");
        if (account.ContainsSyntheticData) warnings.Add("SYNTHETIC EXAMPLE: values are invented test data, not this player's account or verified game mechanics.");
        if (account.Minions.Any(x => x.Evidence.Kind == EvidenceKind.Unverified || x.Evidence.Confidence < 0.8)
            || account.Bonuses.Any(x => x.Evidence.Kind == EvidenceKind.Unverified || x.Evidence.Confidence < 0.8))
            warnings.Add("Some inputs have low confidence or are unverified; inspect their evidence before using the recommendation.");
        var finalists = beam.Concat(baselines).OrderByDescending(x => x.Candidate.Score).ThenByDescending(x => x.Used)
            .DistinctBy(x => string.Join(",", x.Counts)).Take(options.ResultCount);
        return new(finalists.Select(x => EvaluateInternal(account, context, goal, x.Counts)).ToArray(),
            evaluated, limited, sampled, warnings);
    }

    public static TroopCandidate Evaluate(MinionAccount account, BattleContext context, OptimizationGoal goal,
        IReadOnlyDictionary<string, int> counts)
    {
        account.Validate(); ValidateContext(context, goal);
        if (counts.Keys.Any(id => account.Minions.All(m => m.Id != id))) throw new ArgumentException("Composition contains an unknown minion ID.");
        var quantities = account.Minions.Select(m => counts.GetValueOrDefault(m.Id)).ToArray();
        if (quantities.Where((q, i) => q < 0 || q > account.Minions[i].Available).Any() || quantities.Sum(x => (long)x) > account.Leadership)
            throw new ArgumentException("Composition exceeds ready quantities or Leadership.");
        return EvaluateInternal(account, context, goal, quantities);
    }

    public static TroopStats WeightsFor(OptimizationGoal goal) => goal switch
    {
        OptimizationGoal.Damage => new() { Attack = 1, Rush = .1, ElementalDamage = 1 },
        OptimizationGoal.Survivability => new() { Hp = .1, Defense = 1, ElementalResistance = 1 },
        OptimizationGoal.MinimumLossExposure => new() { Hp = .15, Defense = 1.5, ElementalResistance = 1 },
        OptimizationGoal.Speed => new() { Attack = 1, Rush = 1, ElementalDamage = .5 },
        OptimizationGoal.ElementalDamage => new() { ElementalDamage = 1, Attack = .05 },
        OptimizationGoal.ElementalDefense => new() { ElementalResistance = 1, Hp = .01, Defense = .1 },
        _ => new() { Hp = .02, Attack = 1, Defense = .5, Rush = .35, ElementalDamage = .5, ElementalResistance = .25 }
    };

    internal static double Weighted(TroopStats stats, TroopStats weights) => stats.Values.Zip(weights.Values).Sum(p => p.First * p.Second);

    private static void ValidateContext(BattleContext context, OptimizationGoal goal)
    {
        if (context is null || !Enum.IsDefined(context.Mode) || !Enum.IsDefined(goal)) throw new ArgumentException("Unknown mode or goal.");
        if (context.ScoreWeights is not null && context.ScoreWeights.Values.Any(x => !double.IsFinite(x) || x < 0 || x > 1e12))
            throw new ArgumentException("Weights must be finite and nonnegative.");
    }

    private static TroopCandidate EvaluateInternal(MinionAccount account, BattleContext context, OptimizationGoal goal, int[] counts, bool describe = true)
    {
        var formStats = new Dictionary<MinionForm, TroopStats>();
        var formCounts = new Dictionary<MinionForm, int>();
        var composition = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < counts.Length; i++)
        {
            if (counts[i] == 0) continue;
            var minion = account.Minions[i];
            if (describe) composition[minion.Id] = counts[i];
            formStats[minion.Form] = formStats.GetValueOrDefault(minion.Form, new()) + minion.Stats * counts[i];
            formCounts[minion.Form] = formCounts.GetValueOrDefault(minion.Form) + counts[i];
        }
        int used = counts.Sum();
        var globalPercent = new TroopStats();
        var formPercent = new Dictionary<MinionForm, TroopStats>();
        var flat = new TroopStats();
        var active = new List<string>();
        foreach (var bonus in account.Bonuses)
        {
            if (bonus.Mode.HasValue && bonus.Mode != context.Mode) continue;
            int count = bonus.TriggerForm is { } form ? formCounts.GetValueOrDefault(form) : used;
            int stacks = Math.Min(bonus.MaxStacks, bonus.PerGroup ? count / bonus.RequiredCount : count >= bonus.RequiredCount ? 1 : 0);
            if (stacks == 0) continue;
            // A targeted bonus has no effect unless a target is deployed.
            if (bonus.TargetForm is { } target && formCounts.GetValueOrDefault(target) == 0) continue;
            if (bonus.TargetForm is { } targetForm)
                formPercent[targetForm] = formPercent.GetValueOrDefault(targetForm, new()) + bonus.Percent * stacks;
            else globalPercent += bonus.Percent * stacks;
            flat += bonus.Flat * stacks;
            if (describe) active.Add($"{bonus.Name} ×{stacks} [{bonus.Evidence.Source}; confidence {bonus.Evidence.Confidence:P0}]");
        }
        var total = flat;
        foreach (var (form, stats) in formStats)
            total += stats.ApplyPercent(globalPercent + formPercent.GetValueOrDefault(form, new()));
        double score = Weighted(total, context.ScoreWeights ?? WeightsFor(goal));
        string explanation = describe ? $"{goal} stat score {score:F2}; {used}/{account.Leadership} Leadership; "
            + $"{composition.Count} minion types; {active.Count} active supplied bonuses. "
            + "This score compares supplied effective stats and is not a win probability or casualty estimate." : "";
        return new(composition, used, total, score, active, explanation);
    }

    private static IReadOnlyList<int> QuantityChoices(MinionAccount account, BattleContext context, int index, int cap,
        int[] counts, int maximum, out bool sampled)
    {
        sampled = cap >= maximum;
        if (!sampled) return Enumerable.Range(0, cap + 1).ToArray();
        var choices = new SortedSet<int> { 0, cap };
        for (int k = 1; k < maximum; k++) choices.Add((int)((long)cap * k / (maximum - 1)));
        int currentTotal = counts.Sum();
        var currentForms = new Dictionary<MinionForm, int>();
        var futureForms = new Dictionary<MinionForm, int>();
        for (int i = 0; i < account.Minions.Count; i++)
        {
            var form = account.Minions[i].Form;
            if (i < index) currentForms[form] = currentForms.GetValueOrDefault(form) + counts[i];
            if (i > index) futureForms[form] = futureForms.GetValueOrDefault(form) + account.Minions[i].Available;
        }
        foreach (var bonus in account.Bonuses)
        {
            if (bonus.Mode.HasValue && bonus.Mode != context.Mode) continue;
            int current = bonus.TriggerForm is { } trigger ? currentForms.GetValueOrDefault(trigger) : currentTotal;
            int first = Math.Max(1, (current / bonus.RequiredCount) + 1);
            for (int group = first; group <= Math.Min(first + 4, bonus.MaxStacks); group++)
            {
                int needed = group * bonus.RequiredCount - current;
                if (!bonus.TriggerForm.HasValue || bonus.TriggerForm == account.Minions[index].Form)
                {
                    if (needed >= 0 && needed <= cap) choices.Add(needed);
                }
                else if (futureForms.GetValueOrDefault(bonus.TriggerForm.Value) >= needed)
                {
                    // Preserve capacity for a later type's threshold, rather than sampling
                    // away a mixed composition before that bonus can activate.
                    int reserved = account.Leadership - currentTotal - needed;
                    if (reserved >= 0 && reserved <= cap) choices.Add(reserved);
                }
            }
        }
        // Threshold values are retained; the absolute bound also limits pathological input sizes.
        if (choices.Count <= maximum + 2500) return choices.ToArray();
        return choices.Take(maximum + 2499).Append(cap).Distinct().ToArray();
    }
}
