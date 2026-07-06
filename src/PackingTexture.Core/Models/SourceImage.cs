using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PackingTexture.Core.Models;

public sealed record SourceImage(
    Guid Id,
    string FileName,
    int Width,
    int Height,
    SourceChannelSet Channels,
    Image<Rgba32> Pixels)
{
    public IReadOnlyList<ChannelId> AvailableChannels => Channels switch
    {
        SourceChannelSet.Red => [ChannelId.R],
        SourceChannelSet.Gray => [ChannelId.Gray],
        SourceChannelSet.Rgb => [ChannelId.R, ChannelId.G, ChannelId.B],
        SourceChannelSet.Rgba => [ChannelId.R, ChannelId.G, ChannelId.B, ChannelId.A],
        _ => [ChannelId.R, ChannelId.G, ChannelId.B, ChannelId.A]
    };
}
