namespace SnailCommander.Core.Observation;

public enum ScreenKind
{
    Unknown, EmulatorHome, Home, Exploration, Research, Minions, Equipment,
    Relics, Partners, Battle, Events, Dialog
}

public enum ScreenOrientation { Portrait, Landscape, Square }

public sealed record TextElement(string Text, double X, double Y, double Width, double Height);

/// <summary>Confidence is a rule score, not a calibrated OCR or model probability.</summary>
public sealed record ScreenClassification(ScreenKind Kind, double Confidence,
    IReadOnlyList<string> Evidence, string RuleVersion = "keywords-v1");

public sealed record ResourceReading(string Name, decimal Quantity, string RawText,
    string Source = "labeled OCR text; unverified account reading");

public sealed record ScreenObservation
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;
    public string Device { get; init; } = "";
    public int Width { get; init; }
    public int Height { get; init; }
    public ScreenOrientation Orientation => Width == Height ? ScreenOrientation.Square
        : Width > Height ? ScreenOrientation.Landscape : ScreenOrientation.Portrait;
    public string Fingerprint { get; init; } = "";
    public string OcrLanguage { get; init; } = "";
    public string Text { get; init; } = "";
    public IReadOnlyList<TextElement> Elements { get; init; } = Array.Empty<TextElement>();
    public ScreenClassification Classification { get; init; } = new(ScreenKind.Unknown, 0, Array.Empty<string>());
    public IReadOnlyList<ResourceReading> Resources { get; init; } = Array.Empty<ResourceReading>();
    public string RecognitionSource { get; init; } = "Windows.Media.Ocr + local text rules";
}

public interface IScreenClassifier
{
    ScreenClassification Classify(string text);
}

public interface IObservationStore
{
    Task SaveAsync(ScreenObservation observation, CancellationToken cancellationToken = default);
    IReadOnlyList<ScreenObservation> LoadRecent(int count = 100);
}
