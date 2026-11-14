using SnailCommander.Core.Observation;

namespace SnailCommander.Core.Automation;

public enum SafeAction { Login, Reward, Notice, RetryLogin }
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
        if (IsLoginFailure(frame))
        {
            string modal = string.Join(" ", frame.Elements.Where(e => e.X > frame.Width * .15 && e.X < frame.Width * .85
                && e.Y > frame.Height * .3 && e.Y < frame.Height * .65).Select(e => e.Text));
            if (!IsLoginFailure(frame with { Text = modal })) return null;
        }
        bool notice = text.Contains("close notice") && (text.Contains("maintenance") || text.Contains("game updates")
            || text.Contains("species war update is here"));
        bool reward = text.Contains("rewards gained") || text.Contains("obtain 1 body");
        bool login = (text.Contains("server") && (text.Contains("ver:") || text.Contains("version")))
            || frame.Elements.Any(e => e.Text.Equals("Ver:", StringComparison.OrdinalIgnoreCase) && e.Y > frame.Height * .9);
        // A notice can overlay the login footer. Modal context takes priority.
        SafeAction? action = IsLoginFailure(frame) ? SafeAction.RetryLogin : notice ? SafeAction.Notice : reward ? SafeAction.Reward : login ? SafeAction.Login : null;
        if (action is null) return null;
        string label = action == SafeAction.RetryLogin ? "retry" : action == SafeAction.Reward ? "ok" : action == SafeAction.Login ? "start" : "tap";
        double minimumY = action == SafeAction.Notice ? .85 : action == SafeAction.Login ? .65 : .45;
        double maximumY = action == SafeAction.Notice ? .96 : .88;
        var targets = frame.Elements.Where(e => e.Text.Trim().Equals(label, StringComparison.OrdinalIgnoreCase)
            && e.Width > 0 && e.Height > 0 && e.X >= 0 && e.Y >= frame.Height * minimumY
            && e.Y + e.Height <= frame.Height * maximumY
            && e.X + e.Width <= frame.Width && e.Y + e.Height <= frame.Height
            && (action != SafeAction.RetryLogin || (e.X + e.Width / 2 >= frame.Width * .35 && e.X + e.Width / 2 <= frame.Width * .65))).ToArray();
        if (targets.Length != 1) return null;
        var target = targets[0];
        return new(action.Value, (int)(target.X + target.Width / 2), (int)(target.Y + target.Height / 2));
    }

    public static bool Verified(PlannedTap action, ScreenObservation before, ScreenObservation after, string package)
    {
        if (package != GamePackage || before.Origin != ObservationOrigin.LiveAdb || after.Origin != ObservationOrigin.LiveAdb || after.Device != before.Device
            || after.CapturedAt <= before.CapturedAt || after.Fingerprint == before.Fingerprint) return false;
        if (IsLoginFailure(after)) return false;
        bool home = IsLiveHome(after, package, before.Device, after.CapturedAt);
        var next = Plan(after, package, before.Device, after.CapturedAt);
        if (action.Action == SafeAction.RetryLogin && !home && next?.Action is not (SafeAction.Login or SafeAction.Notice)) return false;
        // Unknown pixels and a successful ADB command are not proof of completion.
        if (!home && after.Classification.Kind == ScreenKind.Unknown
            && !(action.Action is SafeAction.Notice or SafeAction.RetryLogin && Plan(after, package, before.Device, after.CapturedAt)?.Action == SafeAction.Login)) return false;
        string text = after.Text.ToLowerInvariant();
        bool remains = action.Action switch
        {
            SafeAction.Reward => text.Contains("rewards gained") || text.Contains("obtain 1 body"),
            SafeAction.Login => Plan(after, package, before.Device, after.CapturedAt)?.Action == SafeAction.Login,
            SafeAction.Notice => text.Contains("close notice"),
            SafeAction.RetryLogin => IsLoginFailure(after),
            _ => true
        };
        return !remains;
    }
    public static bool IsLoginFailure(ScreenObservation frame)
    {
        string text = System.Text.RegularExpressions.Regex.Replace(frame.Text.ToLowerInvariant(), @"[^a-z0-9]+", " ");
        return text.Contains("login verification failed") || text.Contains("login failed")
            || text.Contains("failed to connect to server") || text.Contains("connection to server failed");
    }

    public static bool IsLiveHome(ScreenObservation frame, string package, string device, DateTimeOffset now)
    {
        if (frame.Origin != ObservationOrigin.LiveAdb || frame.Device != device || package != GamePackage
            || frame.Width >= frame.Height || now - frame.CapturedAt > TimeSpan.FromSeconds(30) || frame.CapturedAt > now
            || IsLoginFailure(frame)) return false;
        string text = frame.Text.ToLowerInvariant();
        if (text.Contains("close notice") || text.Contains("rewards gained") || text.Contains("obtain 1 body")) return false;
        // Restrict anchors to the left task board, away from chat and modal action buttons.
        string board = string.Join(" ", frame.Elements.Where(e => e.X >= 0 && e.X < frame.Width * .5
            && e.Y > frame.Height * .1 && e.Y < frame.Height * .4).Select(e => e.Text)).ToLowerInvariant();
        return new[] { "offering", "exploration", "cloning", "healing" }.Count(board.Contains) >= 3;
    }
}
