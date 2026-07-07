using PackingTexture.Core.Models;

namespace PackingTexture.Cli;

internal sealed record CliOptions(
    IReadOnlyList<string> InputPaths,
    string? OutputPath,
    ExportFormat Format,
    bool GenerateMipMaps,
    bool FlipY,
    IReadOnlyDictionary<ChannelId, MappingSpec> ManualMappings);

internal sealed record CliParseResult(CliOptions? Options, string? Error, bool ShowHelp)
{
    public static CliParseResult Success(CliOptions options) => new(options, null, ShowHelp: false);

    public static CliParseResult Failure(string error) => new(null, error, ShowHelp: false);

    public static CliParseResult Help() => new(null, null, ShowHelp: true);
}

internal sealed record MappingSpec(int? SourceIndex, ChannelId? SourceChannel, ChannelSourceKind? ConstantKind, bool Invert)
{
    public static bool TryParse(string value, out MappingSpec spec, out string error)
    {
        var trimmed = value.Trim();
        var invert = trimmed.EndsWith('!');
        if (invert)
        {
            trimmed = trimmed[..^1].Trim();
        }

        if (TryParseConstant(trimmed, out var constantKind))
        {
            spec = new MappingSpec(null, null, constantKind, invert);
            error = "";
            return true;
        }

        var parts = trimmed.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], out var sourceIndex) || sourceIndex < 0)
        {
            spec = new MappingSpec(null, null, null, false);
            error = $"Invalid mapping '{value}'. Use <source-index>:<channel>, optionally suffixed with !.";
            return false;
        }

        if (!TryParseSourceChannel(parts[1], out var channel))
        {
            spec = new MappingSpec(null, null, null, false);
            error = $"Unsupported source channel '{parts[1]}'. Use r, g, b, a, or gray.";
            return false;
        }

        spec = new MappingSpec(sourceIndex, channel, null, invert);
        error = "";
        return true;
    }

    public ChannelMapping ToMapping(ChannelId outputChannel, IReadOnlyList<SourceImage> sources)
    {
        if (ConstantKind is not null)
        {
            return ChannelMapping.ForConstant(outputChannel, ConstantKind.Value, isAutomatic: false, invert: Invert);
        }

        if (SourceIndex is null || SourceChannel is null)
        {
            throw new InvalidOperationException($"Output channel {outputChannel} has no source mapping.");
        }

        if (SourceIndex.Value >= sources.Count)
        {
            throw new InvalidOperationException(
                $"Output channel {outputChannel} references input #{SourceIndex.Value}, but only {sources.Count} input image(s) were provided.");
        }

        return ChannelMapping.ForSource(
            outputChannel,
            sources[SourceIndex.Value].Id,
            SourceChannel.Value,
            isAutomatic: false,
            invert: Invert);
    }

    private static bool TryParseConstant(string value, out ChannelSourceKind constantKind)
    {
        switch (value.ToLowerInvariant())
        {
            case "0":
            case "zero":
                constantKind = ChannelSourceKind.Zero;
                return true;
            case "1":
            case "one":
                constantKind = ChannelSourceKind.One;
                return true;
            default:
                constantKind = default;
                return false;
        }
    }

    private static bool TryParseSourceChannel(string value, out ChannelId channel)
    {
        switch (value.ToLowerInvariant())
        {
            case "r":
            case "red":
                channel = ChannelId.R;
                return true;
            case "g":
            case "green":
                channel = ChannelId.G;
                return true;
            case "b":
            case "blue":
                channel = ChannelId.B;
                return true;
            case "a":
            case "alpha":
                channel = ChannelId.A;
                return true;
            case "gray":
            case "grey":
                channel = ChannelId.Gray;
                return true;
            default:
                channel = default;
                return false;
        }
    }
}
