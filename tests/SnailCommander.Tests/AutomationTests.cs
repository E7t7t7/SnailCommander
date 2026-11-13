using SnailCommander.Core.Automation;
using SnailCommander.Core.Observation;

namespace SnailCommander.Tests;

internal static class AutomationTests
{
    public static void Run(Action<bool, string> assert)
    {
        var now = DateTimeOffset.UtcNow;
        var frame = new ScreenObservation { Device = "127.0.0.1:5555", Origin = ObservationOrigin.LiveAdb,
            CapturedAt = now, Width = 540, Height = 960, Text = "Rewards Gained", Fingerprint = "AA",
            Elements = new[] { new TextElement("OK", 200, 600, 140, 45) } };
        PlannedTap? Plan(ScreenObservation f, string package = AutomationPolicy.GamePackage, string device = "127.0.0.1:5555") => AutomationPolicy.Plan(f, package, device, now);
        var tap = Plan(frame);
        assert(tap is { Action: SafeAction.Reward, X: 270, Y: 622 }, "reward uses observed button bounds");
        assert(Plan(frame, "other.package") is null, "foreign foreground denied");
        assert(Plan(frame, device: "other") is null, "different device denied");
        foreach (var origin in new[] { ObservationOrigin.ImportedImage, ObservationOrigin.Replay, ObservationOrigin.Synthetic, ObservationOrigin.Unspecified })
            assert(Plan(frame with { Origin = origin }) is null, "nonlive source denied " + origin);
        assert(Plan(frame with { CapturedAt = now.AddSeconds(-31) }) is null, "stale source denied");
        assert(Plan(frame with { CapturedAt = now.AddSeconds(1) }) is null, "future source denied");
        foreach (string word in new[] { "Buy", "Spend", "Confirm", "Purchase", "Diamonds", "Terms", "Captcha" })
            assert(Plan(frame with { Text = "Rewards Gained " + word }) is null, "restricted modal denied " + word);
        assert(Plan(frame with { Text = "Unknown OK" }) is null, "generic OK denied");
        assert(Plan(frame with { Elements = Array.Empty<TextElement>() }) is null, "no guessed coordinates");
        assert(Plan(frame with { Elements = new[] { new TextElement("OK", 10, 600, 20, 30), new TextElement("OK", 50, 600, 20, 30) } }) is null, "ambiguous targets denied");
        assert(Plan(frame with { Elements = new[] { new TextElement("OK", 530, 600, 140, 45) } }) is null, "out of bounds denied");
        assert(Plan(frame with { Text = "Server Version 1", Elements = new[] { new TextElement("Start", 200, 700, 140, 45) } })?.Action == SafeAction.Login, "login requires context and exact button");
        assert(Plan(frame with { Text = "Battle Start", Elements = new[] { new TextElement("Start", 200, 700, 140, 45) } }) is null, "battle start denied");
        assert(Plan(frame with { Text = "Game Updates Maintenance Tap anywhere to close notice!", Elements = new[] { new TextElement("Tap", 60, 850, 40, 30) } })?.Action == SafeAction.Notice, "explicit notice dismissal allowed");
        assert(Plan(frame with { Text = "Tap anywhere", Elements = new[] { new TextElement("Tap", 60, 850, 40, 30) } }) is null, "unscoped tap instruction denied");
        var login = frame with { Text = "Ver:", Elements = new[] { new TextElement("Ver:", 10, 910, 40, 20), new TextElement("Start", 200, 700, 140, 45) } };
        assert(Plan(login)?.Action == SafeAction.Login, "exact bottom version footer and Start identify login");
        assert(Plan(login with { Text = "Battle Start Ver:" }) is null, "battle context overrides version footer");
        var notice = frame with { Text = "Game Updates Maintenance Tap anywhere to close notice! Server Version 1", Elements = new[] { new TextElement("Tap", 60, 850, 40, 30) } };
        assert(Plan(notice)?.Action == SafeAction.Notice, "notice overlay takes precedence over login footer");
        assert(AutomationPolicy.Verified(new(SafeAction.Notice, 80, 860), notice, login with { CapturedAt = now.AddSeconds(2), Fingerprint = "BB" }, AutomationPolicy.GamePackage), "notice to explicit login signature verifies even without whole-screen classifier");
        var after = frame with { CapturedAt = now.AddSeconds(2), Text = "Research", Fingerprint = "BB", Classification = new(ScreenKind.Research, .8, new[] { "fixture" }) };
        assert(AutomationPolicy.Verified(tap!, frame, after, AutomationPolicy.GamePackage), "known distinct postcondition verified");
        assert(!AutomationPolicy.Verified(tap!, frame, after with { Classification = new(ScreenKind.Unknown, 0, Array.Empty<string>()) }, AutomationPolicy.GamePackage), "unknown postcondition denied");
        assert(!AutomationPolicy.Verified(tap!, frame, after with { Text = "Rewards Gained" }, AutomationPolicy.GamePackage), "missing OK alone not completion");
        assert(!AutomationPolicy.Verified(tap!, frame, after with { Fingerprint = "AA" }, AutomationPolicy.GamePackage), "unchanged pixels denied");
        assert(!AutomationPolicy.Verified(tap!, frame, after with { Device = "other" }, AutomationPolicy.GamePackage), "crossdevice postcondition denied");
        assert(!AutomationPolicy.Verified(tap!, frame, after, "other"), "foreign postcondition denied");
    }
}
