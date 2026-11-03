using SnailCommander.Core.Updates;

namespace SnailCommander;

/// <summary>Configure once at process startup, before initializing forms or local stores.</summary>
internal static class AppPaths
{
    private static string? _customRoot;
    public static string DataDirectory => _customRoot ?? UpdateWorkerOptions.DefaultDataRoot;
    public static bool UsesCustomRoot => _customRoot is not null;
    public static string CapturesDirectory => UsesCustomRoot ? Path.Combine(DataDirectory, "Captures")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "SnailCommander", "Captures");

    public static void Configure(string[] args)
    {
        var indexes = args.Select((value, index) => (value, index)).Where(x => x.value == "--data-root").ToArray();
        if (indexes.Length > 1) throw new ArgumentException("--data-root may be specified only once.");
        if (indexes.Length == 0) return;
        int index = indexes[0].index;
        if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]) || args[index + 1].StartsWith("--"))
            throw new ArgumentException("--data-root requires a directory.");
        var root = Path.GetFullPath(args[index + 1]).TrimEnd(Path.DirectorySeparatorChar);
        if (root == Path.GetPathRoot(root)?.TrimEnd(Path.DirectorySeparatorChar))
            throw new ArgumentException("A drive root cannot be used as a profile directory.");
        string installation = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
        if (root.Equals(installation, StringComparison.OrdinalIgnoreCase)
            || root.StartsWith(installation + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Keep the profile directory outside the application installation so update backups stay separate.");
        _customRoot = root.Equals(UpdateWorkerOptions.DefaultDataRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
            ? null : root;
    }
}
