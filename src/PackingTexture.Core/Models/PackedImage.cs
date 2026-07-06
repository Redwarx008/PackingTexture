using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PackingTexture.Core.Models;

public sealed class PackedImage : IDisposable
{
    public PackedImage(Image<Rgba32> pixels, bool hadResizedSources)
    {
        Pixels = pixels;
        HadResizedSources = hadResizedSources;
    }

    public Image<Rgba32> Pixels { get; }

    public int Width => Pixels.Width;

    public int Height => Pixels.Height;

    public bool HadResizedSources { get; }

    public void Dispose() => Pixels.Dispose();
}
