using PackingTexture.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PackingTexture.App.ViewModels;

public static class PreviewImageFactory
{
    private const int CheckerSize = 8;
    private static readonly Rgba32 CheckerLight = new(204, 204, 204, 255);
    private static readonly Rgba32 CheckerDark = new(153, 153, 153, 255);

    public static Image<Rgba32> Create(PackedImage packed, PreviewMode mode)
    {
        var clone = packed.Pixels.Clone();
        if (mode == PreviewMode.Rgba)
        {
            CompositeOverCheckerboard(clone);
            return clone;
        }

        for (var y = 0; y < clone.Height; y++)
        {
            for (var x = 0; x < clone.Width; x++)
            {
                var pixel = clone[x, y];
                var value = mode switch
                {
                    PreviewMode.R => pixel.R,
                    PreviewMode.G => pixel.G,
                    PreviewMode.B => pixel.B,
                    PreviewMode.A => pixel.A,
                    _ => pixel.R
                };

                clone[x, y] = new Rgba32(value, value, value, 255);
            }
        }

        return clone;
    }

    private static void CompositeOverCheckerboard(Image<Rgba32> image)
    {
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var pixel = image[x, y];
                var background = GetCheckerColor(x, y);

                if (pixel.A == byte.MaxValue)
                {
                    continue;
                }

                if (pixel.A == 0)
                {
                    image[x, y] = background;
                    continue;
                }

                image[x, y] = new Rgba32(
                    Blend(pixel.R, background.R, pixel.A),
                    Blend(pixel.G, background.G, pixel.A),
                    Blend(pixel.B, background.B, pixel.A),
                    255);
            }
        }
    }

    private static Rgba32 GetCheckerColor(int x, int y) =>
        ((x / CheckerSize) + (y / CheckerSize)) % 2 == 0
            ? CheckerLight
            : CheckerDark;

    private static byte Blend(byte foreground, byte background, byte alpha)
    {
        var inverseAlpha = 255 - alpha;
        var value = (foreground * alpha) + (background * inverseAlpha) + 127;
        return (byte)(value / 255);
    }
}
