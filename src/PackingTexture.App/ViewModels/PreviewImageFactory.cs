using PackingTexture.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PackingTexture.App.ViewModels;

public static class PreviewImageFactory
{
    public static Image<Rgba32> Create(PackedImage packed, PreviewMode mode)
    {
        var clone = packed.Pixels.Clone();
        if (mode == PreviewMode.Rgba)
        {
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
}
