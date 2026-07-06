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
}
