using PackingTexture.Core.Models;
using PackingTexture.Core.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PackingTexture.Core.Tests;

public sealed class TextureExportServiceTests
{
    [Fact]
    public async Task ExportAsync_WritesPng()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        using var pixels = new Image<Rgba32>(2, 2);
        pixels[0, 0] = new Rgba32(1, 2, 3, 255);
        using var packed = new PackedImage(pixels, hadResizedSources: false);

        await TextureExportService.ExportAsync(
            packed,
            path,
            new ExportSettings(ExportFormat.Png, GenerateMipMaps: false),
            CancellationToken.None);

        Assert.True(File.Exists(path));
        using var reloaded = await Image.LoadAsync<Rgba32>(path);
        Assert.Equal(2, reloaded.Width);
        Assert.Equal(2, reloaded.Height);
        File.Delete(path);
    }

    [Fact]
    public async Task ExportAsync_RejectsDdsPathWithPngFormat()
    {
        using var pixels = new Image<Rgba32>(2, 2);
        using var packed = new PackedImage(pixels, hadResizedSources: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TextureExportService.ExportAsync(
                packed,
                "bad.dds",
                new ExportSettings(ExportFormat.Png, GenerateMipMaps: false),
                CancellationToken.None));

        Assert.Contains("extension", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
