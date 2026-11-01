namespace SnailCommander.Core.Updates;

public static class StartupHealth
{
    public static void Acknowledge(string[] args)
    {
        var pathIndex = Array.IndexOf(args, "--update-health");
        var tokenIndex = Array.IndexOf(args, "--update-token");
        if (pathIndex < 0 || tokenIndex < 0) return;
        if (pathIndex + 1 >= args.Length || tokenIndex + 1 >= args.Length) return;
        var token = args[tokenIndex + 1];
        if (!Guid.TryParseExact(token, "N", out _)) return;
        var path = Path.GetFullPath(args[pathIndex + 1]);
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "SnailCommander")) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(path) != "health-" + token + ".ok") return;
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, token);
        File.Move(temporary, path, overwrite: true);
    }
}
