namespace SnailCommander.Core.Observation;

/// <summary>Testable cadence separate from PNG decoding and OCR. Fingerprints are sampled grayscale pixels.</summary>
public sealed class ObservationCadence
{
    private DateTimeOffset? _lastAttempt;
    private string _device = "";
    private int _width, _height;
    private byte[] _fingerprint = Array.Empty<byte>();
    public TimeSpan MinimumInterval { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan RefreshInterval { get; init; } = TimeSpan.FromSeconds(60);

    public bool ShouldAnalyze(DateTimeOffset now, string device, int width, int height, byte[] fingerprint, bool force = false)
    {
        if (force || _lastAttempt is null || _device != device || _width != width || _height != height) return true;
        var elapsed = now - _lastAttempt.Value;
        if (elapsed < TimeSpan.Zero || elapsed >= RefreshInterval) return true;
        return elapsed >= MinimumInterval && MeaningfullyChanged(_fingerprint, fingerprint);
    }

    public void MarkAttempt(DateTimeOffset now, string device, int width, int height, byte[] fingerprint)
    {
        _lastAttempt = now;
        _device = device;
        _width = width;
        _height = height;
        _fingerprint = fingerprint.ToArray();
    }

    public static bool MeaningfullyChanged(ReadOnlySpan<byte> previous, ReadOnlySpan<byte> next)
    {
        if (previous.Length != next.Length || previous.Length == 0) return true;
        long total = 0;
        int changed = 0;
        for (int i = 0; i < previous.Length; i++)
        {
            int delta = Math.Abs(previous[i] - next[i]);
            total += delta;
            if (delta >= 12) changed++;
        }
        return total / (double)previous.Length >= 2.5 || changed / (double)previous.Length >= .05;
    }
}
