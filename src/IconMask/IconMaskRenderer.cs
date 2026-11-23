using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace IconMask;

public enum IconTone
{
    Light,
    Dark,
}

public static class IconMaskRenderer
{
    /// <summary>
    /// Renders a square, transparent bitmap. The caller owns the result and retains
    /// ownership of the source. Size is measured in pixels.
    /// </summary>
    public static Bitmap Render(Bitmap source, int size, IconTone tone = IconTone.Light,
        bool useOriginal = false, bool highContrast = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        if (tone is not (IconTone.Light or IconTone.Dark))
            throw new ArgumentOutOfRangeException(nameof(tone));

        if (useOriginal || highContrast)
            return DrawSquare(source, size, Color.Transparent, size);

        var (bounds, hasPlate, luminance) = Measure(source);
        if (bounds.IsEmpty)
            return new Bitmap(size, size, PixelFormat.Format32bppPArgb);

        using var foreground = source.Clone(bounds, PixelFormat.Format32bppArgb);
        if (hasPlate)
            return RoundedPlate(foreground, size);

        bool darkPlate = tone == IconTone.Dark;
        bool needsContrast = darkPlate ? luminance <= .30 : luminance >= .80;
        if (needsContrast)
            ImproveContrast(foreground, darkPlate);

        return DrawSquare(foreground, size, darkPlate ? Color.Black : Color.White, size * (20d / 26));
    }

    private static Bitmap DrawSquare(Bitmap source, int size, Color plateColor, double artworkSize)
    {
        var result = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(result);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;

        if (plateColor.A != 0)
        {
            using var path = RoundedPath(size);
            using var brush = new SolidBrush(plateColor);
            graphics.FillPath(brush, path);
        }

        double scale = artworkSize / Math.Max(source.Width, source.Height);
        float width = (float)(source.Width * scale);
        float height = (float)(source.Height * scale);
        var position = new RectangleF((size - width) / 2, (size - height) / 2, width, height);
        graphics.DrawImage(source, position, new RectangleF(0, 0, source.Width, source.Height), GraphicsUnit.Pixel);
        return result;
    }

    private static GraphicsPath RoundedPath(int size)
    {
        float diameter = size * (12f / 26);
        float far = size - diameter;
        var path = new GraphicsPath();
        path.AddArc(0, 0, diameter, diameter, 180, 90);
        path.AddArc(far, 0, diameter, diameter, 270, 90);
        path.AddArc(far, far, diameter, diameter, 0, 90);
        path.AddArc(0, far, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static void ImproveContrast(Bitmap foreground, bool darkPlate)
    {
        for (int y = 0; y < foreground.Height; y++)
        for (int x = 0; x < foreground.Width; x++)
        {
            var pixel = foreground.GetPixel(x, y);
            if (pixel.A == 0) continue;
            foreground.SetPixel(x, y, Color.FromArgb(pixel.A, Shade(pixel.R), Shade(pixel.G), Shade(pixel.B)));
        }

        int Shade(byte channel) => darkPlate ? (255 + channel) / 2 : channel / 2;
    }

    private static (Rectangle Bounds, bool HasPlate, double Luminance) Measure(Bitmap bitmap)
    {
        int left = bitmap.Width, top = bitmap.Height, right = -1, bottom = -1;
        int solid = 0;
        double luminance = 0;
        for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++)
        {
            var pixel = bitmap.GetPixel(x, y);
            if (pixel.A < 24) continue;
            left = Math.Min(left, x);
            top = Math.Min(top, y);
            right = Math.Max(right, x);
            bottom = Math.Max(bottom, y);
            if (pixel.A < 224) continue;
            solid++;
            luminance += (.2126 * pixel.R + .7152 * pixel.G + .0722 * pixel.B) / 255;
        }
        if (right < 0) return (Rectangle.Empty, false, .5);

        var bounds = Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
        int inset = (int)Math.Floor(Math.Min(bounds.Width, bounds.Height) * .1);
        // Sample close enough to the corners to distinguish a rounded square from a filled circle.
        bool hasPlate = Math.Min(bounds.Width, bounds.Height) >= .88 * Math.Max(bounds.Width, bounds.Height)
            && solid >= .75 * bounds.Width * bounds.Height
            && bitmap.GetPixel(left + inset, top + inset).A >= 224
            && bitmap.GetPixel(right - inset, top + inset).A >= 224
            && bitmap.GetPixel(left + inset, bottom - inset).A >= 224
            && bitmap.GetPixel(right - inset, bottom - inset).A >= 224;
        return (bounds, hasPlate, solid == 0 ? .5 : luminance / solid);
    }

    private static Bitmap RoundedPlate(Bitmap foreground, int size)
    {
        using var square = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
        using (var scale = Graphics.FromImage(square))
        {
            var area = new Rectangle(0, 0, size, size);
            var top = PlateEdgeColor(foreground, top: true);
            var bottom = PlateEdgeColor(foreground, top: false);
            using var backing = new LinearGradientBrush(area, top, bottom, LinearGradientMode.Vertical);
            scale.FillRectangle(backing, area);
            scale.InterpolationMode = InterpolationMode.HighQualityBicubic;
            scale.PixelOffsetMode = PixelOffsetMode.Half;
            double factor = (double)size / Math.Max(foreground.Width, foreground.Height);
            int width = (int)Math.Round(foreground.Width * factor);
            int height = (int)Math.Round(foreground.Height * factor);
            var imageArea = new Rectangle((size - width) / 2, (size - height) / 2, width, height);
            scale.DrawImage(foreground, imageArea, 0, 0, foreground.Width, foreground.Height, GraphicsUnit.Pixel);
        }

        var plate = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
        using var path = RoundedPath(size);
        using var draw = Graphics.FromImage(plate);
        draw.SmoothingMode = SmoothingMode.AntiAlias;
        using var texture = new TextureBrush(square);
        draw.FillPath(texture, path);
        return plate;
    }

    private static Color PlateEdgeColor(Bitmap bitmap, bool top)
    {
        // The median of an edge band favors the background over details in the logo.
        var samples = new List<Color>();
        int rows = Math.Max(1, (bitmap.Height + 7) / 8);
        for (int row = 0; row < rows; row++)
        {
            int y = top ? row : bitmap.Height - 1 - row;
            for (int x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.A >= 248) samples.Add(pixel);
            }
        }
        if (samples.Count == 0)
        {
            int y = top ? rows : bitmap.Height - 1 - rows;
            var pixel = bitmap.GetPixel(bitmap.Width / 2, Math.Clamp(y, 0, bitmap.Height - 1));
            return Color.FromArgb(255, pixel.R, pixel.G, pixel.B);
        }
        int middle = samples.Count / 2;
        return Color.FromArgb(samples.Select(color => color.R).Order().ElementAt(middle),
            samples.Select(color => color.G).Order().ElementAt(middle),
            samples.Select(color => color.B).Order().ElementAt(middle));
    }
}
