using PackingTexture.Core.Models;
using PackingTexture.Core.Services;

namespace PackingTexture.Cli;

public sealed class CliApplication
{
    private readonly Func<string, CancellationToken, Task<SourceImage>> _importAsync;
    private readonly Func<PackedImage, string, ExportSettings, CancellationToken, Task> _exportAsync;

    public CliApplication()
        : this(ImageImportService.ImportAsync, TextureExportService.ExportAsync)
    {
    }

    public CliApplication(
        Func<string, CancellationToken, Task<SourceImage>> importAsync,
        Func<PackedImage, string, ExportSettings, CancellationToken, Task> exportAsync)
    {
        _importAsync = importAsync ?? throw new ArgumentNullException(nameof(importAsync));
        _exportAsync = exportAsync ?? throw new ArgumentNullException(nameof(exportAsync));
    }

    public async Task<int> RunAsync(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var parseResult = CliCommandLine.Parse(args);
        if (parseResult.ShowHelp)
        {
            await output.WriteLineAsync(CliCommandLine.Usage);
            return 0;
        }

        if (parseResult.Error is not null || parseResult.Options is null)
        {
            await error.WriteLineAsync(parseResult.Error ?? "Invalid command line.");
            await error.WriteLineAsync(CliCommandLine.Usage);
            return 2;
        }

        var options = parseResult.Options;
        var inactiveMapping = options.ManualMappings.Keys
            .FirstOrDefault(channel => !ExportFormatChannelPolicy.IsOutputChannelActive(options.Format, channel));
        if (inactiveMapping != default)
        {
            await error.WriteLineAsync(
                $"{FormatLabel(options.Format)} only supports {FormatChannels(options.Format)} output channels.");
            return 2;
        }

        var sources = new List<SourceImage>(options.InputPaths.Count);
        PackedImage? packedImage = null;
        try
        {
            foreach (var path in options.InputPaths)
            {
                sources.Add(await _importAsync(path, cancellationToken));
            }

            var mappings = BuildMappings(options, sources);
            packedImage = ChannelPackingService.Pack(
                sources,
                ExportFormatChannelPolicy.MaskInactiveMappings(options.Format, mappings),
                options.FlipY);

            var outputPath = options.OutputPath ?? InferOutputPath(options.InputPaths, sources, options.Format);
            await _exportAsync(
                packedImage,
                outputPath,
                new ExportSettings(options.Format, options.GenerateMipMaps),
                cancellationToken);
            await output.WriteLineAsync($"Exported {outputPath}");
            return 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await error.WriteLineAsync(ex.Message);
            return 1;
        }
        finally
        {
            packedImage?.Dispose();
            foreach (var source in sources)
            {
                source.Pixels.Dispose();
            }
        }
    }

    private static IReadOnlyList<ChannelMapping> BuildMappings(CliOptions options, IReadOnlyList<SourceImage> sources)
    {
        var mappings = ChannelPackingService.CreateDefaultMappings(sources).ToDictionary(mapping => mapping.OutputChannel);
        foreach (var (outputChannel, spec) in options.ManualMappings)
        {
            mappings[outputChannel] = spec.ToMapping(outputChannel, sources);
        }

        return [mappings[ChannelId.R], mappings[ChannelId.G], mappings[ChannelId.B], mappings[ChannelId.A]];
    }

    private static string InferOutputPath(
        IReadOnlyList<string> inputPaths,
        IReadOnlyList<SourceImage> sources,
        ExportFormat format)
    {
        var firstInputPath = Path.GetFullPath(inputPaths[0]);
        var directory = Path.GetDirectoryName(firstInputPath) ?? Directory.GetCurrentDirectory();
        var names = sources.Select(source => Path.GetFileNameWithoutExtension(source.FileName)).ToArray();
        var baseName = names.Length switch
        {
            0 => "packed",
            1 => names[0],
            _ => FindCommonPrefix(names).TrimEnd(' ', '_', '-', '.') is { Length: >= 3 } prefix ? prefix : names[0]
        };
        var extension = format == ExportFormat.Png ? "png" : "dds";
        return Path.Combine(directory, $"{baseName}.{extension}");
    }

    private static string FindCommonPrefix(IReadOnlyList<string> names)
    {
        var first = names[0];
        var length = first.Length;
        for (var index = 1; index < names.Count; index++)
        {
            length = Math.Min(length, names[index].Length);
            var position = 0;
            while (position < length && first[position] == names[index][position])
            {
                position++;
            }

            length = position;
            if (length == 0)
            {
                break;
            }
        }

        return first[..length];
    }

    private static string FormatLabel(ExportFormat format) => format switch
    {
        ExportFormat.Png => "PNG",
        ExportFormat.DdsBc1 => "DDS BC1",
        ExportFormat.DdsBc3 => "DDS BC3",
        ExportFormat.DdsBc4 => "DDS BC4",
        ExportFormat.DdsBc5 => "DDS BC5",
        ExportFormat.DdsBc7 => "DDS BC7",
        _ => format.ToString()
    };

    private static string FormatChannels(ExportFormat format)
    {
        var channels = ExportFormatChannelPolicy.GetActiveOutputChannels(format)
            .Select(channel => channel.ToString())
            .ToArray();
        return channels.Length switch
        {
            1 => channels[0],
            2 => $"{channels[0]} and {channels[1]}",
            _ => string.Join(", ", channels[..^1]) + $", and {channels[^1]}"
        };
    }
}
