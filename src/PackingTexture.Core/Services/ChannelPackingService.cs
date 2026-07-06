using PackingTexture.Core.Models;

namespace PackingTexture.Core.Services;

public static class ChannelPackingService
{
    private static readonly ChannelId[] OutputOrder = [ChannelId.R, ChannelId.G, ChannelId.B, ChannelId.A];

    public static IReadOnlyList<ChannelMapping> CreateDefaultMappings(IReadOnlyList<SourceImage> sources)
    {
        var sourceChannels = sources
            .SelectMany(source => source.AvailableChannels.Select(channel => (source.Id, Channel: channel)))
            .ToList();

        var result = new List<ChannelMapping>(4);
        for (var index = 0; index < OutputOrder.Length; index++)
        {
            var output = OutputOrder[index];
            if (index < sourceChannels.Count)
            {
                var source = sourceChannels[index];
                result.Add(ChannelMapping.ForSource(output, source.Id, source.Channel));
                continue;
            }

            result.Add(ChannelMapping.ForConstant(
                output,
                output is ChannelId.A ? ChannelSourceKind.One : ChannelSourceKind.Zero));
        }

        return result;
    }
}
