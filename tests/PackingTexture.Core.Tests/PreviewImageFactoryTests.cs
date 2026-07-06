using PackingTexture.App.ViewModels;
using PackingTexture.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PackingTexture.Core.Tests;

public sealed class PreviewImageFactoryTests
{
    [Fact]
    public void Create_RgbaMode_CompositesOverCheckerboard()
    {
        using var pixels = new Image<Rgba32>(16, 1);
        pixels[0, 0] = new Rgba32(0, 0, 0, 0);
        pixels[4, 0] = new Rgba32(200, 0, 0, 128);
        pixels[8, 0] = new Rgba32(10, 20, 30, 255);
        using var packed = new PackedImage(pixels, hadResizedSources: false);

        using var preview = PreviewImageFactory.Create(packed, PreviewMode.Rgba);

        Assert.Equal(new Rgba32(204, 204, 204, 255), preview[0, 0]);
        Assert.Equal(new Rgba32(202, 102, 102, 255), preview[4, 0]);
        Assert.Equal(new Rgba32(10, 20, 30, 255), preview[8, 0]);
    }
}
