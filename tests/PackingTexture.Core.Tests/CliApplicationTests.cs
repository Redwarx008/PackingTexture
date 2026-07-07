using PackingTexture.Cli;
using PackingTexture.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PackingTexture.Core.Tests;

public sealed class CliApplicationTests
{
    [Fact]
    public async Task RunAsync_PacksManualMappingsAndExportsRequestedFormat()
    {
        var color = CreateSourceImage(
            Guid.Parse("aaaaaaaa-1111-1111-1111-aaaaaaaaaaaa"),
            "Grass005_1K-PNG_Color.png",
            SourceChannelSet.Rgb,
            new Rgba32(10, 20, 30, 255));
        var alpha = CreateSourceImage(
            Guid.Parse("bbbbbbbb-2222-2222-2222-bbbbbbbbbbbb"),
            "Grass005_1K-PNG_Displacement.png",
            SourceChannelSet.Gray,
            new Rgba32(40, 40, 40, 255));
        using var colorScope = color;
        using var alphaScope = alpha;
        color.Detach();
        alpha.Detach();
        PackedImage? exported = null;
        string? exportedPath = null;
        ExportSettings? exportedSettings = null;
        var app = new CliApplication(
            importAsync: (path, _) => Task.FromResult(path == "color.png" ? color.Source : alpha.Source),
            exportAsync: (packed, path, settings, _) =>
            {
                exported = new PackedImage(packed.Pixels.Clone(), packed.HadResizedSources);
                exportedPath = path;
                exportedSettings = settings;
                return Task.CompletedTask;
            });

        var exitCode = await app.RunAsync(
            [
                "pack",
                "-i", "color.png",
                "-i", "alpha.png",
                "--r", "0:r",
                "--g", "0:g",
                "--b", "0:b",
                "--a", "1:gray!",
                "--format", "dds-bc7",
                "-o", "C:/tmp/packed.dds",
                "--mipmaps"
            ],
            TextWriter.Null,
            TextWriter.Null,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal("C:/tmp/packed.dds", exportedPath);
        Assert.Equal(new ExportSettings(ExportFormat.DdsBc7, GenerateMipMaps: true), exportedSettings);
        Assert.NotNull(exported);
        Assert.Equal(new Rgba32(10, 20, 30, 215), exported!.Pixels[0, 0]);
        exported.Dispose();
    }

    [Fact]
    public async Task RunAsync_RejectsInactiveChannelMappingForSelectedFormat()
    {
        var source = CreateSourceImage(
            Guid.Parse("cccccccc-3333-3333-3333-cccccccccccc"),
            "Mask_RGB.png",
            SourceChannelSet.Rgb,
            new Rgba32(10, 20, 30, 255));
        using var sourceScope = source;
        var error = new StringWriter();
        var app = new CliApplication(
            importAsync: (_, _) => Task.FromResult(source.Source),
            exportAsync: (_, _, _, _) => throw new InvalidOperationException("Export should not run."));

        var exitCode = await app.RunAsync(
            ["pack", "-i", "mask.png", "--format", "dds-bc5", "--b", "0:b", "-o", "C:/tmp/out.dds"],
            TextWriter.Null,
            error,
            CancellationToken.None);

        Assert.Equal(2, exitCode);
        Assert.Contains("DDS BC5 only supports R and G", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static TestSourceScope CreateSourceImage(Guid id, string fileName, SourceChannelSet channels, Rgba32 color)
    {
        var pixels = new Image<Rgba32>(1, 1);
        pixels[0, 0] = color;
        return new TestSourceScope(new SourceImage(id, fileName, 1, 1, channels, pixels));
    }

    private sealed class TestSourceScope : IDisposable
    {
        private bool _ownsPixels = true;

        public TestSourceScope(SourceImage source)
        {
            Source = source;
        }

        public SourceImage Source { get; }

        public void Detach() => _ownsPixels = false;

        public void Dispose()
        {
            if (_ownsPixels)
            {
                Source.Pixels.Dispose();
            }
        }
    }
}
