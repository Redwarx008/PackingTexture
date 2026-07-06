using PackingTexture.Core.Models;
using PackingTexture.Core.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PackingTexture.Core.Tests;

public sealed class ChannelPackingServiceTests
{
    [Fact]
    public void CreateDefaultMappings_FillsRgbFromFirstImageAndAlphaFromSecond()
    {
        var firstId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var secondId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        using var rgb = new Image<Rgba32>(2, 2);
        using var gray = new Image<Rgba32>(2, 2);

        var sources = new[]
        {
            new SourceImage(firstId, "Mask_RGB.png", 2, 2, SourceChannelSet.Rgb, rgb),
            new SourceImage(secondId, "Alpha.png", 2, 2, SourceChannelSet.Gray, gray)
        };

        var mappings = ChannelPackingService.CreateDefaultMappings(sources);

        Assert.Equal(4, mappings.Count);
        Assert.Equal(ChannelId.R, mappings[0].OutputChannel);
        Assert.Equal(firstId, mappings[0].SourceImageId);
        Assert.Equal(ChannelId.R, mappings[0].SourceChannel);
        Assert.Equal(ChannelId.G, mappings[1].SourceChannel);
        Assert.Equal(ChannelId.B, mappings[2].SourceChannel);
        Assert.Equal(secondId, mappings[3].SourceImageId);
        Assert.Equal(ChannelId.Gray, mappings[3].SourceChannel);
    }

    [Fact]
    public void CreateDefaultMappings_DefaultsMissingRgbToZeroAndAlphaToOne()
    {
        var sourceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        using var redOnly = new Image<Rgba32>(2, 2);
        var sources = new[]
        {
            new SourceImage(sourceId, "OnlyR.png", 2, 2, SourceChannelSet.Red, redOnly)
        };

        var mappings = ChannelPackingService.CreateDefaultMappings(sources);

        Assert.Equal(ChannelId.R, mappings[0].SourceChannel);
        Assert.Equal(ChannelSourceKind.Zero, mappings[1].SourceKind);
        Assert.Equal(ChannelSourceKind.Zero, mappings[2].SourceKind);
        Assert.Equal(ChannelSourceKind.One, mappings[3].SourceKind);
    }

    [Fact]
    public void Pack_UsesManualSourceChannelMapping()
    {
        var sourceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        using var image = new Image<Rgba32>(1, 1);
        image[0, 0] = new Rgba32(10, 20, 30, 40);
        var source = new SourceImage(sourceId, "Source.png", 1, 1, SourceChannelSet.Rgba, image);

        var mappings = new[]
        {
            ChannelMapping.ForSource(ChannelId.R, sourceId, ChannelId.B, isAutomatic: false),
            ChannelMapping.ForSource(ChannelId.G, sourceId, ChannelId.G, isAutomatic: false),
            ChannelMapping.ForConstant(ChannelId.B, ChannelSourceKind.Zero, isAutomatic: false),
            ChannelMapping.ForConstant(ChannelId.A, ChannelSourceKind.One, isAutomatic: false)
        };

        using var packed = ChannelPackingService.Pack([source], mappings, flipGreen: false);

        Assert.Equal(new Rgba32(30, 20, 0, 255), packed.Pixels[0, 0]);
    }

    [Fact]
    public void Pack_FlipGreenInvertsGreenChannel()
    {
        var sourceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        using var image = new Image<Rgba32>(1, 1);
        image[0, 0] = new Rgba32(0, 32, 0, 255);
        var source = new SourceImage(sourceId, "Normal.png", 1, 1, SourceChannelSet.Rgba, image);
        var mappings = new[]
        {
            ChannelMapping.ForConstant(ChannelId.R, ChannelSourceKind.Zero),
            ChannelMapping.ForSource(ChannelId.G, sourceId, ChannelId.G),
            ChannelMapping.ForConstant(ChannelId.B, ChannelSourceKind.Zero),
            ChannelMapping.ForConstant(ChannelId.A, ChannelSourceKind.One)
        };

        using var packed = ChannelPackingService.Pack([source], mappings, flipGreen: true);

        Assert.Equal((byte)223, packed.Pixels[0, 0].G);
    }

    [Fact]
    public void Pack_ResizesNonPrimarySourcesToFirstSourceSize()
    {
        var firstId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var secondId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        using var first = new Image<Rgba32>(2, 2);
        using var second = new Image<Rgba32>(1, 1);
        first[0, 0] = new Rgba32(1, 0, 0, 255);
        second[0, 0] = new Rgba32(0, 99, 0, 255);

        var sources = new[]
        {
            new SourceImage(firstId, "First.png", 2, 2, SourceChannelSet.Rgba, first),
            new SourceImage(secondId, "Second.png", 1, 1, SourceChannelSet.Rgba, second)
        };
        var mappings = new[]
        {
            ChannelMapping.ForSource(ChannelId.R, firstId, ChannelId.R),
            ChannelMapping.ForSource(ChannelId.G, secondId, ChannelId.G),
            ChannelMapping.ForConstant(ChannelId.B, ChannelSourceKind.Zero),
            ChannelMapping.ForConstant(ChannelId.A, ChannelSourceKind.One)
        };

        using var packed = ChannelPackingService.Pack(sources, mappings, flipGreen: false);

        Assert.Equal(2, packed.Width);
        Assert.Equal(2, packed.Height);
        Assert.Equal((byte)99, packed.Pixels[1, 1].G);
    }

    [Fact]
    public void Pack_ThrowsWhenMappingReferencesMissingSource()
    {
        var sourceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var missingSourceId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        using var image = new Image<Rgba32>(1, 1);
        var source = new SourceImage(sourceId, "Source.png", 1, 1, SourceChannelSet.Rgba, image);
        var mappings = new[]
        {
            ChannelMapping.ForSource(ChannelId.R, missingSourceId, ChannelId.R),
            ChannelMapping.ForConstant(ChannelId.G, ChannelSourceKind.Zero),
            ChannelMapping.ForConstant(ChannelId.B, ChannelSourceKind.Zero),
            ChannelMapping.ForConstant(ChannelId.A, ChannelSourceKind.One)
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => ChannelPackingService.Pack([source], mappings, flipGreen: false));

        Assert.Contains(missingSourceId.ToString(), exception.Message);
    }

    [Fact]
    public void Pack_ThrowsWhenManualMappingTargetsUnavailableSourceChannel()
    {
        var sourceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        using var image = new Image<Rgba32>(1, 1);
        var source = new SourceImage(sourceId, "RedOnly.png", 1, 1, SourceChannelSet.Red, image);
        var mappings = new[]
        {
            ChannelMapping.ForSource(ChannelId.R, sourceId, ChannelId.G, isAutomatic: false),
            ChannelMapping.ForConstant(ChannelId.G, ChannelSourceKind.Zero),
            ChannelMapping.ForConstant(ChannelId.B, ChannelSourceKind.Zero),
            ChannelMapping.ForConstant(ChannelId.A, ChannelSourceKind.One)
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => ChannelPackingService.Pack([source], mappings, flipGreen: false));

        Assert.Contains("not available", exception.Message);
        Assert.Contains(ChannelId.G.ToString(), exception.Message);
    }
}
