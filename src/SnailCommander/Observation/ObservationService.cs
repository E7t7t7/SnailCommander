using System.Drawing.Drawing2D;
using System.Runtime.InteropServices.WindowsRuntime;
using SnailCommander.Core.Observation;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace SnailCommander.Observation;

/// <summary>Local Windows OCR on a worker task. One OCR job at a time, change-gated and throttled independently of live preview.</summary>
public sealed class ObservationService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ObservationCadence _cadence = new();
    private readonly IScreenClassifier _classifier = new ScreenClassifier();
    private readonly ObservationStore _store;
    private readonly double _maximumOcrScale;
    private readonly bool _retryUnknownAtLargerScale;
    public ScreenObservation? Latest { get; private set; }
    public string LastStatus { get; private set; } = "Waiting for a frame.";
    public ObservationStore Store => _store;

    public ObservationService(string historyDirectory, double maximumOcrScale = 1, bool retryUnknownAtLargerScale = false)
    {
        if (!double.IsFinite(maximumOcrScale) || maximumOcrScale is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(maximumOcrScale));
        _maximumOcrScale = maximumOcrScale;
        _retryUnknownAtLargerScale = retryUnknownAtLargerScale;
        _store = new ObservationStore(historyDirectory);
        Latest = _store.LoadRecent(1).FirstOrDefault();
    }

    public async Task<ScreenObservation?> ObserveAsync(byte[] png, string device, bool force = false, CancellationToken ct = default,
        ObservationOrigin origin = ObservationOrigin.Unspecified)
    {
        if (png.Length > 32 * 1024 * 1024) throw new InvalidDataException("OCR frame exceeds 32 MB.");
        if (!await _gate.WaitAsync(0, ct)) return null;
        try
        {
            return await Task.Run(async () =>
            {
                ct.ThrowIfCancellationRequested();
                var capturedAt = DateTimeOffset.UtcNow;
                var frame = SampleFrame(png);
                if (!_cadence.ShouldAnalyze(capturedAt, device, frame.Width, frame.Height, frame.Fingerprint, force)) return null;
                _cadence.MarkAttempt(capturedAt, device, frame.Width, frame.Height, frame.Fingerprint);
                LastStatus = "Recognizing text locally...";
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(20));
                var (text, language, elements, inputScale) = await RecognizeAsync(png, frame.Width, frame.Height, _maximumOcrScale, timeout.Token);
                var classification = _classifier.Classify(text);
                int passes = 1;
                string notes = "Native-size recognition (bounded to 1800 pixels).";
                if (_retryUnknownAtLargerScale && _maximumOcrScale == 1 && classification.Kind == ScreenKind.Unknown
                    && Math.Max(frame.Width, frame.Height) < 1800)
                {
                    passes = 2;
                    try
                    {
                        var alternate = await RecognizeAsync(png, frame.Width, frame.Height, 2, timeout.Token);
                        var alternateClassification = _classifier.Classify(alternate.Text);
                        if (alternateClassification.Kind != ScreenKind.Unknown)
                        {
                            (text, language, elements, inputScale) = alternate;
                            classification = alternateClassification;
                            notes = "Enlarged fallback resolved an Unknown screen. One frame, not independent corroboration.";
                        }
                        else notes = "Both sizes remained Unknown; native result retained.";
                    }
                    catch (Exception) when (!ct.IsCancellationRequested)
                    {
                        notes = "Enlarged fallback unavailable; native result retained.";
                    }
                }
                var observation = new ScreenObservation
                {
                    CapturedAt = capturedAt, Device = device, Width = frame.Width, Height = frame.Height,
                    Origin = origin,
                    Fingerprint = Convert.ToHexString(frame.Fingerprint), OcrLanguage = language,
                    OcrInputScale = inputScale,
                    OcrPassCount = passes, OcrNotes = notes,
                    Text = text, Elements = elements,
                    Classification = classification, Resources = ResourceTextParser.Parse(text)
                };
                await _store.SaveAsync(observation, ct);
                Latest = observation;
                LastStatus = $"{observation.Classification.Kind} · {elements.Count} text regions · {language}";
                return observation;
            }, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            LastStatus = "Local OCR exceeded its 20-second deadline.";
            throw new TimeoutException(LastStatus);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LastStatus = "Observation unavailable: " + ex.Message;
            throw;
        }
        finally { _gate.Release(); }
    }

    private static (int Width, int Height, byte[] Fingerprint) SampleFrame(byte[] png)
    {
        using var memory = new MemoryStream(png, writable: false);
        using var image = Image.FromStream(memory);
        if (image.Width is < 1 or > 8192 || image.Height is < 1 or > 8192)
            throw new InvalidDataException("Unsupported screenshot dimensions.");
        using var small = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(small))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            graphics.DrawImage(image, 0, 0, 32, 32);
        }
        var pixels = new byte[1024];
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                var pixel = small.GetPixel(x, y);
                pixels[y * 32 + x] = (byte)((299 * pixel.R + 587 * pixel.G + 114 * pixel.B) / 1000);
            }
        return (image.Width, image.Height, pixels);
    }

    private static async Task<(string Text, string Language, IReadOnlyList<TextElement> Elements, double Scale)> RecognizeAsync(
        byte[] png, int originalWidth, int originalHeight, double maximumScale, CancellationToken ct)
    {
        // Prefer English for the supported text rules; use another installed OCR language if necessary.
        var english = new Language("en-US");
        var engine = OcrEngine.IsLanguageSupported(english) ? OcrEngine.TryCreateFromLanguage(english)
            : OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is null)
            throw new InvalidOperationException("Windows OCR language support is unavailable. Install an OCR language in Windows Settings > Time & language.");

        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(png.AsBuffer()).AsTask(ct);
        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(ct);
        double scale = Math.Min(maximumScale, Math.Min(1800d, OcrEngine.MaxImageDimension) / Math.Max(originalWidth, originalHeight));
        uint width = (uint)Math.Max(1, Math.Round(originalWidth * scale));
        uint height = (uint)Math.Max(1, Math.Round(originalHeight * scale));
        var transform = new BitmapTransform { ScaledWidth = width, ScaledHeight = height };
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            transform, ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage).AsTask(ct);
        var result = await engine.RecognizeAsync(bitmap).AsTask(ct);
        var elements = result.Lines.SelectMany(line => line.Words).Take(2000).Select(word =>
            new TextElement(word.Text, word.BoundingRect.X * originalWidth / width,
                word.BoundingRect.Y * originalHeight / height, word.BoundingRect.Width * originalWidth / width,
                word.BoundingRect.Height * originalHeight / height)).ToArray();
        // WinRT does not expose word confidence. Do not invent one or convert rule scores into OCR probabilities.
        string text = string.Join(Environment.NewLine, result.Lines.Select(line => line.Text));
        if (text.Length > 60_000) text = text[..60_000];
        return (text, engine.RecognizerLanguage.LanguageTag, elements, scale);
    }
}
