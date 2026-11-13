using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using SnailCommander.Core.Accounts;
using SnailCommander.Core.Observation;
using SnailCommander.Observation;

internal static class AccountOcrTests
{
    public static async Task<int> RunAsync(string output)
    {
        Directory.CreateDirectory(output);
        // Programmatically rendered fixture text, not a real game screenshot/account or an OCR accuracy corpus.
        string[] lines = ["Minion cloning", "Minion: Test Minion", "Leadership: 42", "Minion MAX: 30", "Available: 20",
            "Deployed: 3", "Healing: 2", "HP: 1,234", "ATK: 500", "DEF: 250", "RUSH: 75", "Tier: 7", "Form: Demon"];
        using var image = new Bitmap(540, 960);
        using (var graphics = Graphics.FromImage(image))
        using (var font = new Font("Segoe UI", 18))
        {
            graphics.Clear(Color.White);
            for (int i = 0; i < lines.Length; i++) graphics.DrawString(lines[i], font, Brushes.Black, 20, 20 + 64 * i);
        }
        using var memory = new MemoryStream();
        image.Save(memory, ImageFormat.Png);
        await File.WriteAllBytesAsync(Path.Combine(output, "synthetic-minion.png"), memory.ToArray());
        var observer = new ObservationService(Path.Combine(output, "observations"), retryUnknownAtLargerScale: true);
        var observation = await observer.ObserveAsync(memory.ToArray(), "synthetic-ocr-fixture", true, origin: ObservationOrigin.Synthetic)
            ?? throw new Exception("No Windows OCR result.");
        var extracted = AccountEvidenceExtractor.Extract(observation);
        void Require(AccountField field, decimal value)
        {
            if (extracted.Readings.SingleOrDefault(r => r.Field == field)?.Number != value)
                throw new Exception("Windows OCR/extraction fixture failed for " + field + ": " + observation.Text);
        }
        Require(AccountField.Leadership, 42);
        Require(AccountField.MinionMax, 30);
        Require(AccountField.Hp, 1234);
        Require(AccountField.Available, 20);
        if (extracted.Readings.Any(r => r.Field != AccountField.Leadership && r.MinionName != "Test Minion"))
            throw new Exception("Minion identity was not bound to the explicit fixture label.");
        if (observation.Elements.Any(e => e.X + e.Width > 541 || e.Y + e.Height > 961))
            throw new Exception("Enlarged OCR regions did not map back to framebuffer bounds.");
        var persisted = observer.Store.LoadRecent().First(r => r.Id == observation.Id);
        if (persisted.Origin != ObservationOrigin.Synthetic || persisted.OcrInputScale != 1 || persisted.OcrPassCount != 1)
            throw new Exception("OCR scale/provenance did not survive persistence.");
        if (AccountEvidenceProjection.Build([persisted], "synthetic-ocr-fixture", DateTimeOffset.UtcNow).Fields.Count != 0)
            throw new Exception("Synthetic OCR fixture leaked into live account state.");
        await File.WriteAllTextAsync(Path.Combine(output, "report.json"), JsonSerializer.Serialize(new
        {
            source = "Synthetic rendered text only; not live-game accuracy", observation.OcrInputScale,
            classification = observation.Classification.Kind.ToString(), observation.OcrPassCount, readingCount = extracted.Readings.Count,
            checks = 8, failures = 0
        }, ObservationStore.JsonOptions));
        Console.WriteLine($"PASS: real Windows OCR -> {extracted.Readings.Count} synthetic labeled readings; scale {observation.OcrInputScale:F3}; frame bounds/provenance preserved; live account projection excluded fixture.");
        return 0;
    }
}
