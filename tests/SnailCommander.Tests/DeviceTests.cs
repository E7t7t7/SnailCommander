using SnailCommander.Core.Devices;

internal static class DeviceTests
{
    public static void Run(Action<bool, string> assert)
    {
        var serials = AdbDeviceParser.ConnectedSerials("* daemon started successfully *\r\nList of devices attached\r\n127.0.0.1:5555\tdevice\r\nemulator-5554 device product:android\nother offline\nlocked unauthorized\n127.0.0.1:5555 device\n");
        assert(serials.SequenceEqual(new[] { "127.0.0.1:5555", "emulator-5554" }), "ADB selects only authorized connected serials and deduplicates aliases by serial");
        assert(AdbDeviceParser.ConnectedSerials("List of devices attached\r\n").Count == 0, "ADB empty list stays empty");
        assert(AdbDeviceParser.ConnectedSerials("unknown\tunauthorized").Count == 0, "ADB unauthorized device never selected");
    }
}
