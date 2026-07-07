using PackingTexture.Core.Models;

namespace PackingTexture.Core.Services;

public static class ExportFormatChannelPolicy
{
    public static IReadOnlyList<ChannelId> GetActiveOutputChannels(ExportFormat format) => format switch
    {
        ExportFormat.DdsBc1 => [ChannelId.R, ChannelId.G, ChannelId.B],
        ExportFormat.DdsBc4 => [ChannelId.R],
        ExportFormat.DdsBc5 => [ChannelId.R, ChannelId.G],
        _ => [ChannelId.R, ChannelId.G, ChannelId.B, ChannelId.A]
    };

    public static bool IsOutputChannelActive(ExportFormat format, ChannelId channel) =>
        GetActiveOutputChannels(format).Contains(channel);

    public static IReadOnlyList<ChannelMapping> MaskInactiveMappings(
        ExportFormat format,
        IReadOnlyList<ChannelMapping> mappings) =>
        mappings
            .Select(mapping => IsOutputChannelActive(format, mapping.OutputChannel)
                ? mapping
                : ChannelMapping.ForConstant(
                    mapping.OutputChannel,
                    mapping.OutputChannel == ChannelId.A ? ChannelSourceKind.One : ChannelSourceKind.Zero,
                    mapping.IsAutomatic))
            .ToArray();
}
