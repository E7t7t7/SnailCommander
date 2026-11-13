using SnailCommander.Core.Observation;

namespace SnailCommander.Tests;

public static class ObservationTests
{
    public static void Run(Action<bool, string> assert)
    {
        var classifier = new ScreenClassifier();
        assert(classifier.Classify("").Kind == ScreenKind.Unknown, "Empty OCR stays Unknown.");
        assert(classifier.Classify("ATK 500 DEF 200 random text").Kind == ScreenKind.Unknown, "Stats alone cannot identify a screen.");
        var research = classifier.Classify("GENE RESEARCH\nResearch time: 12:30\nSpeedup");
        assert(research.Kind == ScreenKind.Research && research.Confidence >= .65 && research.Evidence.Count >= 2,
            "Gene research is recognized with explicit evidence.");
        assert(classifier.Classify("Search for games & apps\nSystem apps\nSuper Snail\nPopular games to play").Kind == ScreenKind.EmulatorHome,
            "BlueStacks launcher is distinguished from the game's home.");
        assert(classifier.Classify("researcher relicship equipmental minionship").Kind == ScreenKind.Unknown,
            "Partial words do not trigger screen rules.");
        assert(classifier.Classify("Gene research research speed\nEquipment craft equipped").Kind == ScreenKind.Unknown,
            "Conflicting screen evidence is classified Unknown.");
        assert(classifier.Classify("MINION MAX: 50\nLeadership: 120\nCloning\nMinions").Kind == ScreenKind.Minions,
            "Minion screen recognizes capacity terminology.");
        assert(classifier.Classify("Network error\nFailed to connect\nRetry").Kind == ScreenKind.Dialog,
            "Recoverable error dialog has a distinct classification.");

        var resources = ResourceTextParser.Parse("B-Tads: 1,250\nW-Tads 2.5K\nATK 500");
        assert(resources.Count == 2 && resources.Single(r => r.Name == "B-Tads").Quantity == 1250
            && resources.Single(r => r.Name == "W-Tads").Quantity == 2500, "Only labeled resource amounts are extracted.");
        assert(ResourceTextParser.Parse("B-Tads 1,2\nW-Tads 1.2.3\nG-Tads 100\nW-Tads -50").Count == 0,
            "Malformed amounts and unsupported resource labels are ignored.");
        assert(ResourceTextParser.Parse("B-Tads 10\nB-Tads 20").Count == 0, "Conflicting OCR balances are discarded.");
        assert(ResourceTextParser.Parse("B-Tads 10\nB-Tads 10").Count == 1, "Duplicate matching OCR labels yield one reading.");
        assert(ResourceTextParser.Parse("B-Tads 100/200").Count == 0, "Fractional progress does not become a resource balance.");
        assert(ResourceTextParser.Parse("B-Tads 100 / 200\nW-Tads 1 234").Count == 0, "Spaced progress and ambiguous number grouping are ignored.");
        assert(ResourceTextParser.Parse("B-Tads 1e6\nW-Tads 9999999999999999999999999999999999999B").Count == 0,
            "Scientific and overflowing OCR amounts cannot create balances.");

        var now = new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
        var cadence = new ObservationCadence();
        byte[] fingerprint = Enumerable.Repeat((byte)10, 100).ToArray();
        byte[] changed = Enumerable.Repeat((byte)100, 100).ToArray();
        assert(cadence.ShouldAnalyze(now, "device-a", 540, 960, fingerprint), "The first observation runs immediately.");
        cadence.MarkAttempt(now, "device-a", 540, 960, fingerprint);
        assert(!cadence.ShouldAnalyze(now.AddSeconds(2), "device-a", 540, 960, changed), "Preview changes do not bypass the OCR cadence.");
        assert(cadence.ShouldAnalyze(now.AddSeconds(10), "device-a", 540, 960, changed), "Meaningful changes are recognized after the minimum interval.");
        assert(!cadence.ShouldAnalyze(now.AddSeconds(30), "device-a", 540, 960, fingerprint), "Unchanged frames skip OCR.");
        assert(cadence.ShouldAnalyze(now.AddSeconds(60), "device-a", 540, 960, fingerprint), "Periodic refresh detects small text changes.");
        assert(cadence.ShouldAnalyze(now.AddSeconds(1), "device-b", 540, 960, fingerprint), "Device switches reset observation context.");
        assert(cadence.ShouldAnalyze(now.AddSeconds(1), "device-a", 960, 540, fingerprint), "Resolution and orientation changes trigger recognition.");
        assert(cadence.ShouldAnalyze(now.AddSeconds(1), "device-a", 540, 960, fingerprint, force: true), "Manual replay can force recognition.");
        assert(!ObservationCadence.MeaningfullyChanged(new byte[] { 10, 10 }, new byte[] { 11, 11 }), "Minor grayscale noise is suppressed.");

        var path = Path.Combine(Path.GetTempPath(), "snail-observation-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ObservationStore(path, retainedCount: 2);
            var first = MakeObservation(now, "Gene Research\nResearch time 12:00\nB-Tads 50") with
            { Origin = ObservationOrigin.Synthetic, OcrInputScale = 1.875, OcrPassCount = 2, OcrNotes = "Synthetic metadata round trip" };
            store.SaveAsync(first).GetAwaiter().GetResult();
            var loaded = new ObservationStore(path, retainedCount: 2).LoadRecent();
            assert(loaded.Count == 1 && loaded[0].Id == first.Id && loaded[0].Text == first.Text
                && loaded[0].Orientation == ScreenOrientation.Portrait, "Observation history round-trips across store instances.");
            assert(loaded[0].Origin == ObservationOrigin.Synthetic && loaded[0].OcrInputScale == 1.875
                && loaded[0].OcrPassCount == 2 && loaded[0].OcrNotes == first.OcrNotes,
                "Observation provenance and OCR diagnostics survive restart without being promoted to live data.");
            foreach (var invalid in new[] { first with { Origin = (ObservationOrigin)99 }, first with { OcrInputScale = 0 }, first with { OcrPassCount = 3 } })
            {
                bool rejected = false;
                try { store.SaveAsync(invalid).GetAwaiter().GetResult(); } catch (ArgumentException) { rejected = true; }
                assert(rejected, "Invalid observation provenance/processing metadata is rejected.");
            }
            store.SaveAsync(MakeObservation(now.AddSeconds(1), "Minion MAX 30\nLeadership 60")).GetAwaiter().GetResult();
            store.SaveAsync(MakeObservation(now.AddSeconds(2), "Battle results\nVictory")).GetAwaiter().GetResult();
            assert(Directory.GetFiles(path, "*.json").Length == 2 && store.LoadRecent()[0].CapturedAt == now.AddSeconds(2),
                "History retention is bounded and newest records load first.");
            File.WriteAllText(Path.Combine(path, "zzz-corrupt.json"), "{ incomplete");
            File.WriteAllText(Path.Combine(path, "zzz-crash.json.tmp"), "{ incomplete");
            File.WriteAllText(Path.Combine(path, "zzz-null.json"), "{\"Width\":540,\"Height\":960,\"Classification\":{\"Kind\":0,\"Confidence\":0,\"Evidence\":null}}");
            assert(store.LoadRecent().Count == 2, "Corrupt and uncommitted records do not hide valid history.");
            bool invalidRejected = false;
            try { store.SaveAsync(first with { Elements = new TextElement[] { null! } }).GetAwaiter().GetResult(); }
            catch (ArgumentException) { invalidRejected = true; }
            assert(invalidRejected, "Invalid structured records are rejected before persistence.");
            var replay = ObservationReplay.Run(new[] { MakeObservation(now.AddMinutes(1), "Battle results\nVictory"), first }, classifier);
            assert(replay.Count == 2 && replay[0].Classification.Kind == ScreenKind.Research
                && replay[0].Resources.Single().Quantity == 50 && replay[0].CapturedAt == now,
                "Replay reclassifies recorded OCR and preserves chronological evidence.");
        }
        finally
        {
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.GetFiles(path)) File.Delete(file);
                Directory.Delete(path);
            }
        }
    }

    private static ScreenObservation MakeObservation(DateTimeOffset capturedAt, string text) => new()
    {
        CapturedAt = capturedAt, Device = "fixture-device", Width = 540, Height = 960,
        Text = text, RecognitionSource = "Synthetic deterministic test fixture"
    };
}
