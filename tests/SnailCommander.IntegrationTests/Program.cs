using System.Diagnostics;
using System.Text.Json;
using SnailCommander;
using SnailCommander.Core.Observation;
using SnailCommander.Observation;

// Explicit integration runner: --image <path> or --adb <path> --device <serial>.
// Runs real Windows OCR; emulator integration is optional and never implied by fixture tests.
string? Value(string key)
{
    int i = Array.IndexOf(args, key);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}
try
{
    string output = Path.GetFullPath(Value("--output") ?? Path.Combine(AppContext.BaseDirectory, "integration-output"));
    Directory.CreateDirectory(output);
    var observer = new ObservationService(Path.Combine(output, "observations"));
    byte[] png;
    string device;
    if (Value("--image") is { } image)
    {
        png = await File.ReadAllBytesAsync(image);
        device = "Fixture: " + Path.GetFileName(image);
    }
    else if (Value("--adb") is { } adb && Value("--device") is { } serial)
    {
        var devices = await AdbService.GetDevicesAsync(adb);
        if (!devices.Contains(serial)) throw new Exception("Requested emulator is not connected.");
        png = await AdbService.CaptureAsync(adb, serial);
        device = serial;
        await File.WriteAllBytesAsync(Path.Combine(output, "live.png"), png);
    }
    else throw new ArgumentException("Specify --image <path> or --adb <path> --device <serial>.");
    var watch = Stopwatch.StartNew();
    var observation = await observer.ObserveAsync(png, device, true) ?? throw new Exception("No OCR result.");
    if (observation.Width <= 0 || observation.Height <= 0 || observation.Elements.Count == 0)
        throw new Exception("OCR did not produce dimensions and readable text regions.");
    if (observation.Elements.Any(e => e.X < 0 || e.Y < 0 || e.X + e.Width > observation.Width + 1 || e.Y + e.Height > observation.Height + 1))
        throw new Exception("OCR boxes exceed the actual framebuffer.");
    var replay = ObservationReplay.Run(observer.Store.LoadRecent(), new ScreenClassifier());
    if (replay.Count == 0 || replay[^1].Classification.Kind != observation.Classification.Kind) throw new Exception("Persistence/replay mismatch.");
    var suppressed = await observer.ObserveAsync(png, device);
    if (suppressed is not null) throw new Exception("Unchanged image bypassed cadence gate.");
    await File.WriteAllTextAsync(Path.Combine(output, "report.json"), JsonSerializer.Serialize(observation, ObservationStore.JsonOptions));
    Console.WriteLine($"PASS: {device}, {observation.Width}x{observation.Height} {observation.Orientation}, {observation.Elements.Count} OCR regions, {observation.Classification.Kind}, {watch.ElapsedMilliseconds} ms including persistence and replay.");
    Console.WriteLine("OCR text: " + observation.Text.Replace(Environment.NewLine, " | "));
    return 0;
}
catch (Exception error) { Console.Error.WriteLine(error); return 1; }
