using System.Drawing.Imaging;
using SnailCommander.Core.Observation;

namespace SnailCommander.Observation;

/// <summary>Focused local OCR for pale/orange buttons. Color only proposes crops; exact OCR labels authorize nothing by themselves.</summary>
internal static class AutomationButtonReader
{
    internal static async Task<ScreenObservation> ReadAsync(byte[] png, ScreenObservation frame, CancellationToken ct)
    {
        using var stream = new MemoryStream(png);
        using var bitmap = new Bitmap(stream);
        var elements = frame.Elements.ToList();
        string evidenceText = frame.Text;
        // The dimmed notice dismissal/footer text is often lost in full-frame OCR.
        // These are recognition crops only; targets still come from exact recognized word boxes.
        foreach (var (region, threshold) in new[]
        {
            (new Rectangle(0, bitmap.Height * 87 / 100, bitmap.Width, bitmap.Height * 12 / 100), 100),
            (new Rectangle(0, bitmap.Height * 87 / 100, bitmap.Width, bitmap.Height * 12 / 100), 115),
            (new Rectangle(0, bitmap.Height * 87 / 100, bitmap.Width, bitmap.Height * 12 / 100), 130),
            (new Rectangle(bitmap.Width / 10, bitmap.Height * 28 / 100, bitmap.Width * 8 / 10, bitmap.Height * 45 / 100), 0)
        })
        {
            using var crop = bitmap.Clone(region, PixelFormat.Format32bppArgb);
            if (threshold > 0)
                for (int y = 0; y < crop.Height; y++)
                    for (int x = 0; x < crop.Width; x++)
                    {
                        var c = crop.GetPixel(x, y);
                        bool ink = (c.R + c.G + c.B) / 3 > threshold;
                        crop.SetPixel(x, y, ink ? Color.Black : Color.White);
                    }
            using var bytes = new MemoryStream(); crop.Save(bytes, ImageFormat.Png);
            var read = await ObservationService.RecognizeAsync(bytes.ToArray(), crop.Width, crop.Height, 4, ct);
            evidenceText += "\n" + read.Text;
            elements.AddRange(read.Elements.Where(e => !elements.Any(old => old.Text == e.Text
                && Math.Abs(old.X - (e.X + region.X)) < 10 && Math.Abs(old.Y - (e.Y + region.Y)) < 10))
                .Select(e => e with { X = e.X + region.X, Y = e.Y + region.Y }));
        }
        // No synthetic crop is persisted as a live account observation.
        foreach (var box in Candidates(bitmap).Take(5))
        {
            ct.ThrowIfCancellationRequested();
            using var crop = bitmap.Clone(box, PixelFormat.Format32bppArgb);
            for (int y = 0; y < crop.Height; y++)
                for (int x = 0; x < crop.Width; x++)
                {
                    var c = crop.GetPixel(x, y);
                    crop.SetPixel(x, y, c.R + c.G + c.B < 360 ? Color.Black : Color.White);
                }
            using var bytes = new MemoryStream();
            crop.Save(bytes, ImageFormat.Png);
            var recognized = await ObservationService.RecognizeAsync(bytes.ToArray(), crop.Width, crop.Height, 4, ct);
            string text = recognized.Text.Trim();
            if (text.Equals("OK", StringComparison.OrdinalIgnoreCase) || text.Equals("Start", StringComparison.OrdinalIgnoreCase))
            {
                // Prefer a single region from the real button crop over duplicate whole-frame words.
                elements.RemoveAll(e => e.Text.Equals(text, StringComparison.OrdinalIgnoreCase)
                    && e.X >= box.Left && e.Y >= box.Top && e.X <= box.Right && e.Y <= box.Bottom);
                elements.Add(new(text, box.X, box.Y, box.Width, box.Height));
            }
        }
        return frame with { Text = evidenceText, Elements = elements, OcrNotes = frame.OcrNotes + " Focused button OCR (not account data)." };
    }

    internal static IReadOnlyList<Rectangle> Candidates(Bitmap bitmap)
    {
        const int step = 3;
        int width = bitmap.Width / step, height = bitmap.Height / step;
        var mask = new byte[width * height];
        for (int y = height * 2 / 5; y < height * 19 / 20; y++)
            for (int x = 0; x < width; x++)
            {
                var c = bitmap.GetPixel(x * step, y * step);
                bool orange = c.R > 180 && c.G is > 100 and < 205 && c.B is > 65 and < 190 && c.R - c.G > 20 && c.G - c.B > 8;
                bool pale = c.R > 210 && c.G > 205 && c.B > 185 && Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B)) < 60;
                if (orange || pale) mask[y * width + x] = 1;
            }
        var result = new List<Rectangle>();
        for (int index = 0; index < mask.Length; index++)
        {
            if (mask[index] == 0) continue;
            var queue = new Queue<int>(); queue.Enqueue(index); mask[index] = 0;
            int left = width, right = 0, top = height, bottom = 0, count = 0;
            while (queue.TryDequeue(out int p))
            {
                int x = p % width, y = p / width;
                left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y); count++;
                foreach (var (nx, ny) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
                    if (nx >= 0 && nx < width && ny >= 0 && ny < height && mask[ny * width + nx] == 1)
                    { mask[ny * width + nx] = 0; queue.Enqueue(ny * width + nx); }
            }
            var box = new Rectangle(left * step, top * step, (right - left + 1) * step, (bottom - top + 1) * step);
            if (box.Width > bitmap.Width * .25 && box.Width < bitmap.Width * .9
                && box.Height > bitmap.Height * .025 && box.Height < bitmap.Height * .15
                && box.Width > box.Height * 2 && count > 100)
                result.Add(box);
        }
        return result.OrderByDescending(b => b.Top).ToArray();
    }
}
