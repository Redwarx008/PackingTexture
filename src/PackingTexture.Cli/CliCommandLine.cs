using PackingTexture.Core.Models;

namespace PackingTexture.Cli;

internal static class CliCommandLine
{
    public const string Usage =
        """
        Usage:
          PackingTexture.Cli pack -i <image> [-i <image> ...] --format <png|dds-bc1|dds-bc3|dds-bc4|dds-bc5|dds-bc7> [-o <output>] [--r <map>] [--g <map>] [--b <map>] [--a <map>] [--flip-y] [--mipmaps]

        Mapping syntax:
          <source-index>:<r|g|b|a|gray>   Example: --a 1:gray
          <source-index>:<channel>!       Example: --g 0:g!
          0 or 1                          Constant black/white
        """;

    public static CliParseResult Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            return CliParseResult.Help();
        }

        if (!args[0].Equals("pack", StringComparison.OrdinalIgnoreCase))
        {
            return CliParseResult.Failure("Expected command: pack");
        }

        var inputPaths = new List<string>();
        var mappings = new Dictionary<ChannelId, MappingSpec>();
        string? outputPath = null;
        ExportFormat? format = null;
        var mipmaps = false;
        var flipY = false;

        for (var index = 1; index < args.Count; index++)
        {
            var token = args[index];
            switch (token.ToLowerInvariant())
            {
                case "-i":
                case "--input":
                    if (!TryReadValue(args, ref index, token, out var inputValue, out var inputError))
                    {
                        return CliParseResult.Failure(inputError);
                    }

                    inputPaths.Add(inputValue);
                    break;

                case "-o":
                case "--output":
                    if (!TryReadValue(args, ref index, token, out outputPath, out var outputError))
                    {
                        return CliParseResult.Failure(outputError);
                    }

                    break;

                case "--format":
                    if (!TryReadValue(args, ref index, token, out var formatValue, out var formatError))
                    {
                        return CliParseResult.Failure(formatError);
                    }

                    if (!TryParseFormat(formatValue, out var parsedFormat))
                    {
                        return CliParseResult.Failure($"Unsupported format: {formatValue}");
                    }

                    format = parsedFormat;
                    break;

                case "--r":
                case "--g":
                case "--b":
                case "--a":
                    if (!TryReadValue(args, ref index, token, out var mappingValue, out var mappingReadError))
                    {
                        return CliParseResult.Failure(mappingReadError);
                    }

                    if (!MappingSpec.TryParse(mappingValue, out var mappingSpec, out var mappingParseError))
                    {
                        return CliParseResult.Failure(mappingParseError);
                    }

                    mappings[ParseOutputChannel(token)] = mappingSpec;
                    break;

                case "--mipmaps":
                    mipmaps = true;
                    break;

                case "--no-mipmaps":
                    mipmaps = false;
                    break;

                case "--flip-y":
                    flipY = true;
                    break;

                default:
                    return CliParseResult.Failure($"Unknown option: {token}");
            }
        }

        if (inputPaths.Count == 0)
        {
            return CliParseResult.Failure("At least one input image is required.");
        }

        if (format is null)
        {
            return CliParseResult.Failure("--format is required.");
        }

        return CliParseResult.Success(new CliOptions(inputPaths, outputPath, format.Value, mipmaps, flipY, mappings));
    }

    private static bool TryReadValue(
        IReadOnlyList<string> args,
        ref int index,
        string option,
        out string value,
        out string error)
    {
        if (index + 1 >= args.Count)
        {
            value = "";
            error = $"{option} requires a value.";
            return false;
        }

        value = args[++index];
        error = "";
        return true;
    }

    private static ChannelId ParseOutputChannel(string token) => token.ToLowerInvariant() switch
    {
        "--r" => ChannelId.R,
        "--g" => ChannelId.G,
        "--b" => ChannelId.B,
        "--a" => ChannelId.A,
        _ => throw new InvalidOperationException($"Unsupported output channel option {token}.")
    };

    private static bool TryParseFormat(string value, out ExportFormat format)
    {
        switch (value.ToLowerInvariant())
        {
            case "png":
                format = ExportFormat.Png;
                return true;
            case "bc1":
            case "dds-bc1":
            case "ddsbc1":
                format = ExportFormat.DdsBc1;
                return true;
            case "bc3":
            case "dds-bc3":
            case "ddsbc3":
                format = ExportFormat.DdsBc3;
                return true;
            case "bc4":
            case "dds-bc4":
            case "ddsbc4":
                format = ExportFormat.DdsBc4;
                return true;
            case "bc5":
            case "dds-bc5":
            case "ddsbc5":
                format = ExportFormat.DdsBc5;
                return true;
            case "bc7":
            case "dds-bc7":
            case "ddsbc7":
                format = ExportFormat.DdsBc7;
                return true;
            default:
                format = default;
                return false;
        }
    }
}
