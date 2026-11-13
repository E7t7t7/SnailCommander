using SnailCommander.Core.Observation;

namespace SnailCommander.Core.Automation;

public enum SafeAction { Login, Reward, Notice }
public sealed record PlannedTap(SafeAction Action, int X, int Y);

/// <summary>Small explicit allowlist. OCR is evidence, never an instruction source.</summary>
public static class AutomationPolicy
{
    public const string GamePackage = "com.qcplay.snail.android.na";
    public static PlannedTap? Plan(ScreenObservation frame, string package, string device, DateTimeOffset now)
    {
        if (frame.Origin != ObservationOrigin.LiveAdb || frame.Device != device || package != GamePackage
            || now - frame.CapturedAt > TimeSpan.FromSeconds(30) || frame.CapturedAt > now
            || frame.Width < 100 || frame.Height < 100) return null;
        string text = frame.Text.ToLowerInvariant();
        if (new[] { "purchase", "buy", "spend", "confirm", "payment", "privacy", "terms", "captcha", "diamonds", "premium", "challenge", "fight", "battle start" }
            .Any(text.Contains)) return null;
        bool notice = text.Contains("close notice") && (text.Contains("maintenance") || text.Contains("game updates")
            || text.Contains("species war update is here"));
        bool reward = text.Contains("rewards gained") || text.Contains("obtain 1 body");
        bool login = (text.Contains("server") && (text.Contains("ver:") || text.Contains("version")))
            || frame.Elements.Any(e => e.Text.Equals("Ver:", StringComparison.OrdinalIgnoreCase) && e.Y > frame.Height * .9);
        // A notice can overlay the login footer. Modal context takes priority.
        SafeAction? action = notice ? SafeAction.Notice : reward ? SafeAction.Reward : login ? SafeAction.Login : null;
        if (action is null) return null;
        string label = action == SafeAction.Reward ? "ok" : action == SafeAction.Login ? "start" : "tap";
        double minimumY = action == SafeAction.Notice ? .85 : action == SafeAction.Login ? .65 : .45;
        double maximumY = action == SafeAction.Notice ? .96 : .88;
        var targets = frame.Elements.Where(e => e.Text.Trim().Equals(label, StringComparison.OrdinalIgnoreCase)
            && e.Width > 0 && e.Height > 0 && e.X >= 0 && e.Y >= frame.Height * minimumY
            && e.Y + e.Height <= frame.Height * maximumY
            && e.X + e.Width <= frame.Width && e.Y + e.Height <= frame.Height).ToArray();
        if (targets.Length != 1) return null;
        var target = targets[0];
        return new(action.Value, (int)(target.X + target.Width / 2), (int)(target.Y + target.Height / 2));
    }

    public static bool Verified(PlannedTap action, ScreenObservation before, ScreenObservation after, string package)
    {
        if (package != GamePackage || before.Origin != ObservationOrigin.LiveAdb || after.Origin != ObservationOrigin.LiveAdb || after.Device != before.Device
            || after.CapturedAt <= before.CapturedAt || after.Fingerprint == before.Fingerprint) return false;
        // Unknown pixels and a successful ADB command are not proof of completion.
        if (after.Classification.Kind == ScreenKind.Unknown
            && !(action.Action == SafeAction.Notice && Plan(after, package, before.Device, after.CapturedAt)?.Action == SafeAction.Login)) return false;
        string text = after.Text.ToLowerInvariant();
        bool remains = action.Action switch
        {
            SafeAction.Reward => text.Contains("rewards gained") || text.Contains("obtain 1 body"),
            SafeAction.Login => Plan(after, package, before.Device, after.CapturedAt)?.Action == SafeAction.Login,
            SafeAction.Notice => text.Contains("close notice"),
            _ => true
        };
        return !remains;
    }
}
