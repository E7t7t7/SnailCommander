using System.Globalization;
using System.Text.RegularExpressions;
using SnailCommander.Core.Observation;

namespace SnailCommander.Core.Accounts;

public enum AccountField { Leadership, MinionMax, Available, Deployed, Healing, Hp, Attack, Defense, Rush, Tier, Form, BTadsReading, WTadsReading }
public enum ReadingState { Single, Repeated, Rounded, Unassigned, Conflicting, Stale }

public sealed record AccountReading(AccountField Field, string? MinionName, string Scope,
    decimal? Number, string? TextValue, bool Rounded, string RawText,
    string ObservationId, DateTimeOffset CapturedAt, string Device, ObservationOrigin Origin)
{
    public string Value => Number?.ToString(CultureInfo.InvariantCulture) ?? TextValue ?? "";
}

public sealed record AccountExtraction(IReadOnlyList<AccountReading> Readings, IReadOnlyList<string> Issues,
    string RuleVersion = "labeled-account-v1");

/// <summary>Extracts labeled readings, never infers balances from icons, missing counts, or upgrade previews.</summary>
public static class AccountEvidenceExtractor
{
    private static readonly Regex Label = new(@"^(?<label>Leadership|Minion\s+MAX|Available|Ready|Deployed|Healing|HP|ATK|DEF|RUSH|Tier|Form)(?:\s*[:：]\s*|\s+)(?<value>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex Name = new(@"^(?:Minion(?:\s+Name)?|Name)\s*[:：]\s*(?<name>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex Number = new(@"^(?<value>(?:\d+|\d{1,3}(?:,\d{3})+)(?:\.\d+)?)(?<suffix>[kmb])?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex Resource = new(@"^(?<label>[bw]-?tads?)(?:\s*[:：]\s*|\s+)(?<value>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Dictionary<string, AccountField> Fields = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Leadership"] = AccountField.Leadership, ["Minion MAX"] = AccountField.MinionMax,
        ["Available"] = AccountField.Available, ["Ready"] = AccountField.Available,
        ["Deployed"] = AccountField.Deployed, ["Healing"] = AccountField.Healing,
        ["HP"] = AccountField.Hp, ["ATK"] = AccountField.Attack, ["DEF"] = AccountField.Defense,
        ["RUSH"] = AccountField.Rush, ["Tier"] = AccountField.Tier, ["Form"] = AccountField.Form
    };

    public static AccountExtraction Extract(ScreenObservation observation, IEnumerable<string>? knownMinionNames = null)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var readings = new List<AccountReading>();
        var issues = new List<string>();
        if (observation.Text is null || observation.Text.Length >= 60_000 || observation.Classification is null
            || string.IsNullOrWhiteSpace(observation.Id) || string.IsNullOrWhiteSpace(observation.Device))
            return new(readings, ["Observation is incomplete or exceeds the extraction text limit."]);
        var kind = observation.Classification.Kind;
        if (kind is ScreenKind.Unknown or ScreenKind.EmulatorHome or ScreenKind.Dialog or ScreenKind.Events)
            return new(readings, ["This screen has no supported account-reading context."]);
        string[] lines = observation.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Take(2000).Select(Normalize).Where(l => l.Length > 0).ToArray();
        string? minion = null;
        string scope = "Visible labeled resource (not an established balance)";
        if (kind == ScreenKind.Minions)
        {
            var names = lines.Select(l => Name.Match(l)).Where(m => m.Success).Select(m => m.Groups["name"].Value.Trim())
                .Where(n => n.Length is > 0 and <= 100).Concat((knownMinionNames ?? [])
                    .Where(n => !string.IsNullOrWhiteSpace(n) && n.Length <= 100 && lines.Any(l => l.Equals(Normalize(n), StringComparison.OrdinalIgnoreCase))))
                .Distinct(StringComparer.OrdinalIgnoreCase).Take(3).ToArray();
            if (names.Length == 1) minion = names[0];
            else issues.Add(names.Length > 1 ? "Multiple minion names are visible; individual readings remain unassigned." : "No unambiguous minion name is established.");
            scope = lines.Any(l => l.Contains("Minion Sim", StringComparison.OrdinalIgnoreCase)) ? "Minions (mentions Minion Simulation)"
                : lines.Any(l => l.Equals("Species War", StringComparison.OrdinalIgnoreCase)) ? "Minions (mentions Species War)"
                : "Minions (mode unspecified)";
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                // A label and a standalone value on the following OCR line are supported.
                if (Fields.ContainsKey(line.TrimEnd(':', '：').TrimEnd()) && i + 1 < lines.Length)
                    line = line.TrimEnd(':', '：').TrimEnd() + ": " + lines[i + 1];
                var match = Label.Match(line);
                if (!match.Success) continue;
                string label = Normalize(match.Groups["label"].Value);
                if (!Fields.TryGetValue(label, out var field)) continue;
                string value = match.Groups["value"].Value;
                string? entity = field == AccountField.Leadership ? null : minion;
                if (field == AccountField.Form)
                {
                    if (Enum.TryParse<Minions.MinionForm>(value, true, out var form) && Enum.IsDefined(form)
                        && form is not (Minions.MinionForm.Unknown or Minions.MinionForm.Other))
                        readings.Add(Make(field, entity, null, form.ToString(), false, line));
                    else issues.Add("Unsupported form text: " + value[..Math.Min(80, value.Length)]);
                    continue;
                }
                bool integral = field is AccountField.Leadership or AccountField.MinionMax or AccountField.Available
                    or AccountField.Deployed or AccountField.Healing or AccountField.Tier;
                if (!TryNumber(value, integral, out decimal number, out bool rounded))
                {
                    issues.Add("Ambiguous or unsupported " + label + " reading."); continue;
                }
                readings.Add(Make(field, entity, number, null, rounded, line));
            }
        }
        foreach (string line in lines)
        {
            var resource = Resource.Match(line);
            if (resource.Success && TryNumber(resource.Groups["value"].Value, false, out decimal quantity, out bool rounded))
                readings.Add(Make(resource.Groups["label"].Value.StartsWith('b') || resource.Groups["label"].Value.StartsWith('B')
                    ? AccountField.BTadsReading : AccountField.WTadsReading, null, quantity, null, rounded, line,
                    "Visible labeled resource (not an established balance)"));
        }
        if (readings.Count > 100) return new([], ["Too many labeled readings; extraction was rejected rather than truncating conflicting evidence."]);
        return new(readings.ToArray(), issues.Distinct().Take(30).ToArray());

        AccountReading Make(AccountField field, string? entity, decimal? number, string? text, bool rounded, string raw, string? explicitScope = null)
            => new(field, entity, explicitScope ?? scope, number, text, rounded, raw[..Math.Min(raw.Length, 512)],
                observation.Id, observation.CapturedAt, observation.Device, observation.Origin);
    }

    private static bool TryNumber(string text, bool integral, out decimal value, out bool rounded)
    {
        value = 0; rounded = false;
        var match = Number.Match(text.Trim());
        if (!match.Success) return false;
        string suffix = match.Groups["suffix"].Value.ToLowerInvariant();
        if (integral && suffix.Length > 0) return false;
        if (!decimal.TryParse(match.Groups["value"].Value.Replace(",", ""), NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out decimal parsed)) return false;
        decimal scale = suffix switch { "k" => 1000, "m" => 1_000_000, "b" => 1_000_000_000, _ => 1 };
        if (parsed > 1_000_000_000_000m / scale) return false;
        value = parsed * scale; rounded = suffix.Length > 0;
        return !integral || value == decimal.Truncate(value) && value <= 100000;
    }

    private static string Normalize(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}

public sealed record AccountFieldEvidence(AccountField Field, string? MinionName, string Scope,
    string? Value, ReadingState State, int MatchingFrames, DateTimeOffset LastSeen, IReadOnlyList<AccountReading> Evidence);

public sealed record AccountEvidenceSnapshot(string Device, DateTimeOffset ComputedAt, int MaximumAgeSeconds,
    IReadOnlyList<AccountFieldEvidence> Fields, IReadOnlyList<string> Warnings, string RuleVersion = "labeled-account-v1");

public static class AccountEvidenceProjection
{
    public static AccountEvidenceSnapshot Build(IEnumerable<ScreenObservation> history, string device, DateTimeOffset now,
        IEnumerable<string>? knownMinionNames = null, TimeSpan? maximumAge = null)
    {
        TimeSpan age = maximumAge ?? TimeSpan.FromMinutes(10);
        if (age < TimeSpan.FromSeconds(1) || age > TimeSpan.FromDays(1)) throw new ArgumentOutOfRangeException(nameof(maximumAge));
        var names = (knownMinionNames ?? []).Take(200).ToArray();
        var frames = history.Take(3000).Where(o => o is not null && o.Device == device && o.Origin == ObservationOrigin.LiveAdb
            && o.CapturedAt <= now.AddSeconds(5)).OrderByDescending(o => o.CapturedAt).DistinctBy(o => o.Id).Take(200).ToArray();
        var readings = frames.SelectMany(o => AccountEvidenceExtractor.Extract(o, names).Readings).ToArray();
        var result = new List<AccountFieldEvidence>();
        bool Individual(AccountField key) => key is not (AccountField.Leadership or AccountField.BTadsReading or AccountField.WTadsReading);
        foreach (var group in readings.GroupBy(r => (r.Field, Name: r.MinionName?.ToUpperInvariant(), r.Scope,
            UnboundFrame: Individual(r.Field) && r.MinionName is null ? r.ObservationId : "")))
        {
            // Simultaneous contradictory records have no trustworthy "latest" ordering.
            var ordered = group.GroupBy(r => r.CapturedAt).OrderByDescending(g => g.Key).ToArray();
            var latest = ordered[0].ToArray();
            var first = latest[0];
            // Unassigned cards are displayed only for the newest observed frame, never combined across cards.
            if (Individual(first.Field) && first.MinionName is null && first.ObservationId != frames.FirstOrDefault()?.Id) continue;
            bool conflict = latest.Select(r => r.Value).Distinct().Count() > 1;
            int matches = 0;
            var times = new HashSet<DateTimeOffset>();
            foreach (var frame in ordered)
            {
                if (frame.Any(r => r.Value != first.Value || r.Rounded != first.Rounded) || frame.Select(r => r.Value).Distinct().Count() != 1) break;
                if (now - frame.First().CapturedAt <= age && times.Add(frame.First().CapturedAt)) matches++;
            }
            var state = now - first.CapturedAt > age ? ReadingState.Stale
                : conflict ? ReadingState.Conflicting
                : Individual(first.Field) && first.MinionName is null ? ReadingState.Unassigned
                : latest.Any(r => r.Rounded) ? ReadingState.Rounded
                : matches >= 2 ? ReadingState.Repeated : ReadingState.Single;
            result.Add(new(first.Field, first.MinionName, first.Scope, conflict ? null : first.Value, state,
                conflict ? 0 : matches, first.CapturedAt, ordered.Take(3).SelectMany(g => g).Take(12).ToArray()));
        }
        return new(device, now, (int)age.TotalSeconds, result.OrderBy(r => r.MinionName).ThenBy(r => r.Field).ToArray(),
            ["Readings are OCR evidence, not verified account balances or authorization to act.",
             "Device identity does not prove account identity. Imported, replayed, synthetic and legacy observations are excluded.",
             "Missing fields remain unknown. Repeated means matching live frames, not a calibrated confidence probability."]);
    }
}
