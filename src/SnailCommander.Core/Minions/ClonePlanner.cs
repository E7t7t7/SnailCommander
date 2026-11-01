namespace SnailCommander.Core.Minions;

public sealed record ResourceBudget
{
    public Dictionary<string, double> Balances { get; init; } = new();
    public Dictionary<string, double> Reserves { get; init; } = new();
}

public sealed record CloneQueueItem(string MinionId, string Name, int Quantity,
    IReadOnlyDictionary<string, double> Cost, double Minutes, string Reason);

public sealed record ClonePlan(IReadOnlyList<CloneQueueItem> Queue,
    IReadOnlyDictionary<string, double> RemainingBalances, IReadOnlyList<string> Blockers);

/// <summary>
/// Produces an advisory queue against explicit requested troop shortages and reserves.
/// Uses stat gain / (1 + normalized resource pressure + hours) to rank cloning; this is a
/// transparent scheduling heuristic, not a game reward formula. Never spends resources.
/// </summary>
public static class ClonePlanner
{
    public static ClonePlan Plan(MinionAccount account, ResourceBudget budget,
        IReadOnlyDictionary<string, int> desiredReady, OptimizationGoal goal = OptimizationGoal.Balanced,
        int maxCopies = 10000)
    {
        account.Validate();
        if (budget is null || budget.Balances is null || budget.Reserves is null
            || budget.Balances.Concat(budget.Reserves).Any(x => string.IsNullOrWhiteSpace(x.Key) || !double.IsFinite(x.Value) || x.Value < 0)
            || maxCopies is < 1 or > 100000 || !Enum.IsDefined(goal))
            throw new ArgumentException("Invalid resource budget or cloning bounds.");
        if (desiredReady.Any(x => x.Value < 0 || account.Minions.All(m => m.Id != x.Key)))
            throw new ArgumentException("Clone demand contains an unknown minion or negative count.");
        var remaining = new Dictionary<string, double>(budget.Balances);
        var planned = new Dictionary<string, int>();
        var sequence = new List<string>();
        var blockers = new HashSet<string>(StringComparer.Ordinal);
        for (int iteration = 0; iteration < maxCopies; iteration++)
        {
            MinionType? best = null;
            double bestScore = double.NegativeInfinity;
            foreach (var minion in account.Minions)
            {
                int quantity = planned.GetValueOrDefault(minion.Id);
                int shortage = desiredReady.GetValueOrDefault(minion.Id) - minion.Available - quantity;
                if (shortage <= 0) continue;
                if (minion.Owned + quantity >= minion.MinionMax)
                {
                    blockers.Add($"{minion.Name}: Minion MAX reached; healing/deployed copies still occupy its cap."); continue;
                }
                if (minion.CloneCost.Count == 0)
                {
                    blockers.Add($"{minion.Name}: cloning cost is unknown; no free cloning is assumed."); continue;
                }
                if (minion.CloneCost.Any(c => remaining.GetValueOrDefault(c.Key) - budget.Reserves.GetValueOrDefault(c.Key) < c.Value))
                {
                    blockers.Add($"{minion.Name}: insufficient resources after reserves."); continue;
                }
                double pressure = minion.CloneCost.Sum(c => c.Value / Math.Max(1, remaining.GetValueOrDefault(c.Key) - budget.Reserves.GetValueOrDefault(c.Key)));
                double score = TroopOptimizer.Weighted(minion.Stats, TroopOptimizer.WeightsFor(goal)) / (1 + pressure + minion.CloneMinutes / 60);
                if (score > bestScore) { bestScore = score; best = minion; }
            }
            if (best is null) break;
            planned[best.Id] = planned.GetValueOrDefault(best.Id) + 1;
            sequence.Add(best.Id);
            foreach (var cost in best.CloneCost) remaining[cost.Key] = remaining.GetValueOrDefault(cost.Key) - cost.Value;
        }
        var queue = new List<CloneQueueItem>();
        // Preserve the chosen scheduling order, grouping only consecutive copies.
        for (int i = 0; i < sequence.Count;)
        {
            string id = sequence[i];
            int end = i + 1;
            while (end < sequence.Count && sequence[end] == id) end++;
            int count = end - i;
            var m = account.Minions.First(x => x.Id == id);
            queue.Add(new(id, m.Name, count, m.CloneCost.ToDictionary(x => x.Key, x => x.Value * count), m.CloneMinutes * count,
                $"Fill requested ready shortage, respecting Minion MAX and reserves; {goal} stat gain/resource-pressure/time heuristic."));
            i = end;
        }
        if (sequence.Count == maxCopies) blockers.Add("Queue reached its copy limit; remaining demand needs a later planning pass.");
        return new(queue, remaining, blockers.OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }
}
