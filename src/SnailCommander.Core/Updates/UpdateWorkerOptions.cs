namespace SnailCommander.Core.Updates;

public sealed record UpdateWorkerOptions(int WaitPid, string Staging, string Destination,
    string DataRoot, int Retention, int HealthTimeoutSeconds, int HealthGraceSeconds)
{
    public static string DefaultDataRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SnailCommander");

    public static UpdateWorkerOptions Parse(string[] args)
    {
        if (args.Length % 2 != 0) throw new ArgumentException("Updater arguments must be pairs.");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i += 2)
        {
            if (args[i] is not ("--wait-pid" or "--staging" or "--destination" or "--retain"
                or "--data-root" or "--health-timeout" or "--health-grace"))
                throw new ArgumentException("Unexpected updater argument: " + args[i]);
            if (!values.TryAdd(args[i], args[i + 1])) throw new ArgumentException("Duplicate updater argument: " + args[i]);
        }
        if (!values.TryGetValue("--wait-pid", out var text) || !int.TryParse(text, out int pid) || pid <= 0
            || !values.TryGetValue("--staging", out var staging) || string.IsNullOrWhiteSpace(staging)
            || !values.TryGetValue("--destination", out var destination) || string.IsNullOrWhiteSpace(destination))
            throw new ArgumentException("Missing or invalid updater arguments.");
        int Bounded(string key, int fallback, int minimum, int maximum)
        {
            if (!values.TryGetValue(key, out var raw)) return fallback;
            if (!int.TryParse(raw, out int value) || value < minimum || value > maximum)
                throw new ArgumentException($"{key} must be between {minimum} and {maximum}.");
            return value;
        }
        string dataRoot = values.GetValueOrDefault("--data-root", DefaultDataRoot);
        if (string.IsNullOrWhiteSpace(dataRoot)) throw new ArgumentException("Updater data root is empty.");
        return new(pid, Path.GetFullPath(staging), Path.GetFullPath(destination), Path.GetFullPath(dataRoot),
            Bounded("--retain", 3, 1, 10), Bounded("--health-timeout", 45, 1, 120), Bounded("--health-grace", 3, 1, 30));
    }
}
