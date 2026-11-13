namespace SnailCommander.Core.Interaction;

/// <summary>Maps a Zoom picture's client coordinates to framebuffer pixels. Letterboxing never sends input.</summary>
public static class GameViewport
{
    public static (int X, int Y)? Map(int viewWidth, int viewHeight, int imageWidth, int imageHeight, double x, double y)
    {
        if (viewWidth <= 0 || viewHeight <= 0 || imageWidth <= 0 || imageHeight <= 0 || !double.IsFinite(x) || !double.IsFinite(y)) return null;
        float scale = Math.Min((float)viewWidth / imageWidth, (float)viewHeight / imageHeight);
        int width = (int)(imageWidth * scale), height = (int)(imageHeight * scale);
        if (width <= 0 || height <= 0) return null;
        int left = (viewWidth - width) / 2, top = (viewHeight - height) / 2;
        if (x < left || y < top || x >= left + width || y >= top + height) return null;
        return (Math.Min(imageWidth - 1, (int)((x - left) * imageWidth / width)), Math.Min(imageHeight - 1, (int)((y - top) * imageHeight / height)));
    }
}
