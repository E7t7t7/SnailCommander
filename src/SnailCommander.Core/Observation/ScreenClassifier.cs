using System.Globalization;
using System.Text.RegularExpressions;

namespace SnailCommander.Core.Observation;

/// <summary>
/// Conservative English text recognition. These are UI phrase heuristics, not game-mechanics rules.
/// Unknown and ambiguous screens remain Unknown; classifications never authorize input actions.
/// </summary>
public sealed class ScreenClassifier : IScreenClassifier
{
    private sealed record Rule(ScreenKind Kind, string[] Anchors, string[] Supporting);
    private static readonly Rule[] Rules =
    {
        new(ScreenKind.EmulatorHome, new[] { "search for games", "popular games to play" }, new[] { "system apps", "store" }),
        new(ScreenKind.Home, new[] { "home resources", "snail home", "home base" }, new[] { "pond", "mushroom", "mailbox" }),
        new(ScreenKind.Exploration, new[] { "exploration", "intel tree", "intelligence tree" }, new[] { "travel", "intel", "realm", "explore" }),
        new(ScreenKind.Research, new[] { "gene research", "research speed", "research time" }, new[] { "gene", "research", "speedup" }),
        new(ScreenKind.Minions, new[] { "minion cloning", "minion sim", "minion max", "troop deployment", "cloning tank" }, new[] { "leadership", "minions", "cloning", "troop" }),
        new(ScreenKind.Equipment, new[] { "gear forge", "equipment", "gear crafting" }, new[] { "craft", "upgrade", "equipped", "dismantle" }),
        new(ScreenKind.Relics, new[] { "relic awakening", "relic resonance", "relics" }, new[] { "awaken", "fragments", "resonance" }),
        new(ScreenKind.Partners, new[] { "partner skills", "friendship level", "partners" }, new[] { "friendship", "skill", "upgrade" }),
        new(ScreenKind.Battle, new[] { "battle results", "battle report", "combat results", "victory", "defeated" }, new[] { "damage", "round", "enemy", "casualties" }),
        new(ScreenKind.Events, new[] { "lottery event", "wishmaking event", "offering event", "species war", "limited time event" }, new[] { "event", "rewards", "remaining" }),
        new(ScreenKind.Dialog, new[] { "connection lost", "network error", "reconnecting", "failed to connect", "server maintenance" }, new[] { "retry", "confirm", "cancel" })
    };

    public ScreenClassification Classify(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new(ScreenKind.Unknown, 0, new[] { "No readable text." });
        string normalized = Normalize(text);
        var candidates = Rules.Select(rule =>
        {
            var anchors = rule.Anchors.Where(p => ContainsPhrase(normalized, p)).ToArray();
            var supporting = rule.Supporting.Where(p => ContainsPhrase(normalized, p)).ToArray();
            // One anchor, or two independent supporting phrases, is the minimum evidence.
            double score = anchors.Length > 0 ? Math.Min(.95, .65 + .10 * (anchors.Length - 1) + .08 * supporting.Length)
                : supporting.Length >= 2 ? Math.Min(.72, .52 + .05 * supporting.Length) : 0;
            return (rule.Kind, Score: score, Evidence: anchors.Concat(supporting).Distinct().ToArray());
        }).Where(c => c.Score > 0).OrderByDescending(c => c.Score).ToArray();

        if (candidates.Length == 0)
            return new(ScreenKind.Unknown, 0, new[] { "No supported screen phrases matched." });
        if (candidates.Length > 1 && candidates[0].Score - candidates[1].Score < .12)
            return new(ScreenKind.Unknown, candidates[0].Score,
                new[] { $"Ambiguous: {candidates[0].Kind} / {candidates[1].Kind}." }
                    .Concat(candidates[0].Evidence).Concat(candidates[1].Evidence).ToArray());
        return new(candidates[0].Kind, candidates[0].Score, candidates[0].Evidence);
    }

    private static string Normalize(string value) => Regex.Replace(value.ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();
    private static bool ContainsPhrase(string normalized, string phrase) =>
        (" " + normalized + " ").Contains(" " + phrase + " ", StringComparison.Ordinal);
}

/// <summary>Only explicitly labeled, unambiguous B-Tad/W-Tad quantities; no icon inference.</summary>
public static class ResourceTextParser
{
    private static readonly Regex Pattern = new(@"(?<name>\bb-?tads?|\bw-?tads?)\s*[:=]?\s*(?<value>\d[\d,.]*)[ \t]*(?<suffix>[kmb])?(?![\p{L}\d.,/])(?![ \t]*[/\d.,])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
    private static readonly Regex Number = new(@"^(?:\d+|\d{1,3}(?:,\d{3})+)(?:\.\d+)?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static IReadOnlyList<ResourceReading> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length >= 60_000) return Array.Empty<ResourceReading>();
        var readings = new List<ResourceReading>();
        try
        {
            foreach (Match match in Pattern.Matches(text))
            {
                if (!Number.IsMatch(match.Groups["value"].Value)) continue;
                if (!decimal.TryParse(match.Groups["value"].Value.Replace(",", ""), NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out var quantity)) continue;
                decimal scale = match.Groups["suffix"].Value.ToLowerInvariant() switch { "k" => 1000, "m" => 1_000_000, "b" => 1_000_000_000, _ => 1 };
                if (quantity > decimal.MaxValue / scale) continue;
                string name = match.Groups["name"].Value.StartsWith("w", StringComparison.OrdinalIgnoreCase) ? "W-Tads" : "B-Tads";
                readings.Add(new(name, quantity * scale, match.Value));
            }
        }
        catch (RegexMatchTimeoutException) { return Array.Empty<ResourceReading>(); }
        // Conflicting OCR labels cannot establish an account balance.
        return readings.GroupBy(r => r.Name).Where(g => g.Select(r => r.Quantity).Distinct().Count() == 1)
            .Select(g => g.First()).ToArray();
    }
}
