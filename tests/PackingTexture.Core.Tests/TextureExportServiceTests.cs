using PackingTexture.Core.Models;
using PackingTexture.Core.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PackingTexture.Core.Tests;

public sealed class TextureExportServiceTests
{
    [Theory]
    [InlineData(ExportFormat.DdsBc1)]
    [InlineData(ExportFormat.DdsBc3)]
    [InlineData(ExportFormat.DdsBc4)]
    [InlineData(ExportFormat.DdsBc5)]
    [InlineData(ExportFormat.DdsBc7)]
    public async Task ExportAsync_WritesDds_ForAllSupportedFormats(ExportFormat format)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.dds");
        using var pixels = new Image<Rgba32>(4, 4);
        pixels[0, 0] = new Rgba32(1, 2, 3, 255);
        pixels[1, 0] = new Rgba32(40, 50, 60, 255);
        using var packed = new PackedImage(pixels, hadResizedSources: false);

        try
        {
            await TextureExportService.ExportAsync(
                packed,
                path,
                new ExportSettings(format, GenerateMipMaps: false),
                CancellationToken.None);

            Assert.True(File.Exists(path));

            var bytes = await File.ReadAllBytesAsync(path);
            Assert.True(bytes.Length > 128);
            Assert.Equal((byte)'D', bytes[0]);
            Assert.Equal((byte)'D', bytes[1]);
            Assert.Equal((byte)'S', bytes[2]);
            Assert.Equal((byte)' ', bytes[3]);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

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

    [Fact]
    public async Task ExportAsync_DoesNotOverwriteExistingFile_WhenCanceledBeforeWrite()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        var originalBytes = new byte[] { 1, 2, 3, 4, 5, 6 };
        await File.WriteAllBytesAsync(path, originalBytes);

        using var pixels = new Image<Rgba32>(2, 2);
        pixels[0, 0] = new Rgba32(1, 2, 3, 255);
        using var packed = new PackedImage(pixels, hadResizedSources: false);

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                TextureExportService.ExportAsync(
                    packed,
                    path,
                    new ExportSettings(ExportFormat.Png, GenerateMipMaps: false),
                    new CancellationToken(canceled: true)));

            var bytesAfterFailure = await File.ReadAllBytesAsync(path);
            Assert.Equal(originalBytes, bytesAfterFailure);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
