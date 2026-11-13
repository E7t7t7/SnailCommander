using SnailCommander.Core.Accounts;
using SnailCommander.Core.Observation;

internal static class AccountEvidenceTests
{
    // All numeric/name data in this suite is invented. LiveAdb here simulates provenance for deterministic policy tests.
    public static void Run(Action<bool, string> assert)
    {
        var now = new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
        ScreenObservation Frame(string text, int seconds = 0, string device = "fixture-device", ObservationOrigin origin = ObservationOrigin.LiveAdb,
            ScreenKind kind = ScreenKind.Minions) => new()
        {
            Id = Guid.NewGuid().ToString("N"), CapturedAt = now.AddSeconds(seconds), Device = device, Origin = origin,
            Width = 540, Height = 960, Text = text, Classification = new(kind, .8, ["Synthetic policy fixture"])
        };
        const string card = "Minion cloning\nMinion: Fixture Alpha\nLeadership: 40\nMinion MAX: 30\nAvailable: 20\nDeployed: 5\nHealing: 2\nHP: 1,234\nATK: 500\nDEF: 250\nRUSH: 75\nTier: 7\nForm: Demon\nB-Tads: 10K";
        var extraction = AccountEvidenceExtractor.Extract(Frame(card));
        assert(extraction.Readings.Count == 12, "Supported labels extract partial numeric/form readings without inventing missing fields");
        assert(extraction.Readings.Single(r => r.Field == AccountField.Hp).Number == 1234, "English thousands grouping parses correctly");
        assert(extraction.Readings.Single(r => r.Field == AccountField.MinionMax).Number == 30
            && extraction.Readings.Single(r => r.Field == AccountField.Leadership).Number == 40, "Leadership and individual MAX remain separate fields");
        assert(extraction.Readings.Single(r => r.Field == AccountField.Available).MinionName == "Fixture Alpha", "Explicit single name binds individual minion evidence");
        assert(extraction.Readings.Single(r => r.Field == AccountField.BTadsReading) is { Number: 10000, Rounded: true }, "Rounded resource displays retain approximation metadata");
        assert(extraction.Readings.All(r => r.RawText.Length > 0 && r.ObservationId.Length > 0 && r.Origin == ObservationOrigin.LiveAdb), "Every reading retains frame/source evidence");
        foreach (string bad in new[] { "HP: +100", "HP: 10%", "HP: 100 -> 200", "HP: 100/200", "HP: 100 / 200", "HP: 1,20", "HP: 1 234", "HP: -1", "HP: 999999999999999999999999999999999999999", "HP: 100 +50", "HP: 100 cost", "Leadership: 1.5", "Minion MAX: 2K", "Available: 100001" })
            assert(AccountEvidenceExtractor.Extract(Frame("Minion cloning\n" + bad)).Readings.Count == 0, "Reject ambiguous/bonus/unsupported number: " + bad);
        foreach (var kind in new[] { ScreenKind.Unknown, ScreenKind.Events, ScreenKind.Dialog, ScreenKind.EmulatorHome })
            assert(AccountEvidenceExtractor.Extract(Frame(card, kind: kind)).Readings.Count == 0, "Unsupported screen context cannot populate account evidence: " + kind);
        assert(AccountEvidenceExtractor.Extract(Frame("Obtain 1 body(s)!\nRewards Gained\nMemory Module +12345")).Readings.Count == 0,
            "Reward popups and gains do not establish inventory totals");
        assert(AccountEvidenceExtractor.Extract(Frame("Minion cloning\nHP\n125\nAvailable\n0")).Readings.Count == 2,
            "Explicit labels can bind standalone values on the next OCR line, including a real zero");
        assert(AccountEvidenceExtractor.Extract(Frame("Minion cloning\nHP\nATK: 100")).Readings.Count == 1,
            "Adjacent label is not mistaken for a missing number");
        var known = AccountEvidenceExtractor.Extract(Frame("Minion cloning\nFixture Alpha\nHP: 100"), ["Fixture Alpha"]);
        assert(known.Readings.Single().MinionName == "Fixture Alpha", "Exact existing inventory name can bind an otherwise unlabeled card");
        var multi = AccountEvidenceExtractor.Extract(Frame("Minion cloning\nMinion: Fixture Alpha\nMinion: Fixture Beta\nHP: 100"));
        assert(multi.Readings.Single().MinionName is null, "Multiple visible names never assign a card to one guessed minion");
        assert(AccountEvidenceExtractor.Extract(Frame("Minion cloning\n" + new string('x', 60000))).Readings.Count == 0, "Extraction text size is bounded");
        assert(AccountEvidenceExtractor.Extract(Frame("Minion cloning\n" + string.Join('\n', Enumerable.Repeat("HP: 100", 101)))).Readings.Count == 0,
            "Too many readings are rejected rather than hiding conflicts past a truncation limit");
        var first = Frame(card, -10); var second = Frame(card);
        AccountEvidenceSnapshot Project(params ScreenObservation[] frames) => AccountEvidenceProjection.Build(frames, "fixture-device", now);
        var repeated = Project(first, second).Fields.Single(f => f.Field == AccountField.Hp);
        assert(repeated.State == ReadingState.Repeated && repeated.MatchingFrames == 2 && repeated.Value == "1234", "Matching live frames produce repeat evidence");
        assert(Project(second).Fields.Single(f => f.Field == AccountField.Hp).State == ReadingState.Single, "One frame remains a single reading");
        assert(Project(first, first).Fields.Single(f => f.Field == AccountField.Hp).MatchingFrames == 1, "Duplicate observation IDs cannot manufacture repeats");
        assert(Project(second, second with { Id = Guid.NewGuid().ToString("N") }).Fields.Single(f => f.Field == AccountField.Hp).MatchingFrames == 1,
            "Duplicate capture timestamps cannot manufacture repeats");
        var simultaneous = Project(Frame("Minion cloning\nMinion: Fixture Alpha\nHP: 100"), Frame("Minion cloning\nMinion: Fixture Alpha\nHP: 200")).Fields.Single();
        assert(simultaneous.State == ReadingState.Conflicting && simultaneous.Value is null,
            "Contradictory simultaneous frames have no invented latest ordering");
        var conflict = Project(Frame("Minion cloning\nMinion: Fixture Alpha\nHP: 100\nHP: 200")).Fields.Single();
        assert(conflict.State == ReadingState.Conflicting && conflict.Value is null && conflict.MatchingFrames == 0, "Same-frame conflicts remove the apparent value");
        var resourceConflict = Project(Frame("B-Tads: 100\nB-Tads: 200")).Fields.Single();
        assert(resourceConflict.State == ReadingState.Conflicting, "Resource conflicts remain explicit instead of resurrecting an old reading");
        var changed = Project(first, Frame(card.Replace("HP: 1,234", "HP: 999"))).Fields.Single(f => f.Field == AccountField.Hp);
        assert(changed.State == ReadingState.Single && changed.Value == "999", "Changed latest value resets prior corroboration");
        var stale = Project(Frame(card, -601)).Fields.Single(f => f.Field == AccountField.Hp);
        assert(stale.State == ReadingState.Stale && stale.MatchingFrames == 0, "Expired evidence remains visibly stale rather than becoming a current value");
        assert(Project(Frame(card, device: "other-device")).Fields.Count == 0, "Evidence from other devices is never merged");
        foreach (var origin in new[] { ObservationOrigin.Unspecified, ObservationOrigin.ImportedImage, ObservationOrigin.Replay, ObservationOrigin.Synthetic })
            assert(Project(Frame(card, origin: origin)).Fields.Count == 0, "Non-live provenance cannot populate live evidence: " + origin);
        assert(Project(Frame(card, 10)).Fields.Count == 0, "Future-dated readings cannot revive stale state");
        assert(Project(Frame("Minion cloning\nHP: 100", -10), Frame("Minion cloning\nHP: 200")).Fields.Single().State == ReadingState.Unassigned,
            "Unnamed cards are not merged across minions");
        var rounded = Project(Frame("Minion cloning\nMinion: Fixture Alpha\nHP: 1K", -10), Frame("Minion cloning\nMinion: Fixture Alpha\nHP: 1000")).Fields.Single();
        assert(rounded.State == ReadingState.Single && rounded.MatchingFrames == 1, "Rounded earlier values cannot corroborate exact precision");
        var sim = Project(Frame("Minion Sim\nLeadership: 30", -10), Frame("Minion cloning\nLeadership: 40"));
        assert(sim.Fields.Count == 2, "Explicit simulation context is not merged with an unspecified minion mode");
        var replay = ObservationReplay.Run([second], new ScreenClassifier()).Single();
        assert(replay.Origin == ObservationOrigin.Replay && replay.CapturedAt == second.CapturedAt, "Replay preserves original age and marks replay provenance");
    }
}
