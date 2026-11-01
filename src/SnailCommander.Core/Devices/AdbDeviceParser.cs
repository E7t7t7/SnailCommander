namespace SnailCommander.Core.Devices;

public static class AdbDeviceParser
{
    public static IReadOnlyList<string> ConnectedSerials(string output) => output
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        .Where(parts => parts.Length >= 2 && parts[1] == "device")
        .Select(parts => parts[0]).Distinct(StringComparer.Ordinal).ToArray();
}
