namespace PackingTexture.Core.Models;

public sealed record ChannelMapping(
    ChannelId OutputChannel,
    ChannelSourceKind SourceKind,
    Guid? SourceImageId,
    ChannelId? SourceChannel,
    bool IsAutomatic)
{
    public static ChannelMapping ForSource(
        ChannelId output,
        Guid sourceId,
        ChannelId sourceChannel,
        bool isAutomatic = true) =>
        new(output, ChannelSourceKind.SourceChannel, sourceId, sourceChannel, isAutomatic);

    public static ChannelMapping ForConstant(
        ChannelId output,
        ChannelSourceKind constantKind,
        bool isAutomatic = true)
    {
        if (constantKind is ChannelSourceKind.SourceChannel)
        {
            throw new ArgumentException("Use ForSource for source-channel mappings.", nameof(constantKind));
        }

        return new(output, constantKind, null, null, isAutomatic);
    }
}
