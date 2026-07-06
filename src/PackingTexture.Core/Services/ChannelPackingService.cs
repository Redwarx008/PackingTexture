using PackingTexture.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

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

    public static PackedImage Pack(
        IReadOnlyList<SourceImage> sources,
        IReadOnlyList<ChannelMapping> mappings,
        bool flipGreen)
    {
        if (sources.Count == 0)
        {
            throw new InvalidOperationException("Add at least one source image before packing.");
        }

        if (mappings.Count != 4)
        {
            throw new InvalidOperationException("Exactly four output channel mappings are required.");
        }

        var width = sources[0].Width;
        var height = sources[0].Height;
        var hadResizedSources = sources.Any(source => source.Width != width || source.Height != height);
        var sourceLookup = sources.ToDictionary(source => source.Id);
        var redMapping = GetRequiredMapping(mappings, ChannelId.R);
        var greenMapping = GetRequiredMapping(mappings, ChannelId.G);
        var blueMapping = GetRequiredMapping(mappings, ChannelId.B);
        var alphaMapping = GetRequiredMapping(mappings, ChannelId.A);
        ValidateMapping(redMapping, sourceLookup);
        ValidateMapping(greenMapping, sourceLookup);
        ValidateMapping(blueMapping, sourceLookup);
        ValidateMapping(alphaMapping, sourceLookup);

        using var resizedSources = new DisposableImageLookup(sources, width, height);
        Image<Rgba32>? output = null;

        try
        {
            output = new Image<Rgba32>(width, height);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var r = ResolveByte(redMapping, resizedSources, x, y);
                    var g = ResolveByte(greenMapping, resizedSources, x, y);
                    var b = ResolveByte(blueMapping, resizedSources, x, y);
                    var a = ResolveByte(alphaMapping, resizedSources, x, y);

                    if (flipGreen)
                    {
                        g = (byte)(255 - g);
                    }

                    output[x, y] = new Rgba32(r, g, b, a);
                }
            }

            return new PackedImage(output, hadResizedSources);
        }
        catch
        {
            output?.Dispose();
            throw;
        }
    }

    private static ChannelMapping GetRequiredMapping(IReadOnlyList<ChannelMapping> mappings, ChannelId outputChannel) =>
        mappings.Single(mapping => mapping.OutputChannel == outputChannel);

    private static void ValidateMapping(ChannelMapping mapping, IReadOnlyDictionary<Guid, SourceImage> sourceLookup)
    {
        if (mapping.SourceKind != ChannelSourceKind.SourceChannel)
        {
            return;
        }

        if (mapping.SourceImageId is null || mapping.SourceChannel is null)
        {
            throw new InvalidOperationException($"Output channel {mapping.OutputChannel} has no source.");
        }

        if (!sourceLookup.ContainsKey(mapping.SourceImageId.Value))
        {
            throw new InvalidOperationException(
                $"Output channel {mapping.OutputChannel} references missing source image {mapping.SourceImageId.Value}.");
        }

        var source = sourceLookup[mapping.SourceImageId.Value];
        if (!source.AvailableChannels.Contains(mapping.SourceChannel.Value))
        {
            throw new InvalidOperationException(
                $"Output channel {mapping.OutputChannel} maps source image {mapping.SourceImageId.Value} channel {mapping.SourceChannel.Value}, but that channel is not available on the source image.");
        }
    }

    private static byte ResolveByte(ChannelMapping mapping, DisposableImageLookup images, int x, int y)
    {
        if (mapping.SourceKind == ChannelSourceKind.Zero)
        {
            return 0;
        }

        if (mapping.SourceKind == ChannelSourceKind.One)
        {
            return 255;
        }

        if (mapping.SourceImageId is null || mapping.SourceChannel is null)
        {
            throw new InvalidOperationException($"Output channel {mapping.OutputChannel} has no source.");
        }

        var pixel = images[mapping.SourceImageId.Value][x, y];
        return mapping.SourceChannel.Value switch
        {
            ChannelId.R => pixel.R,
            ChannelId.G => pixel.G,
            ChannelId.B => pixel.B,
            ChannelId.A => pixel.A,
            ChannelId.Gray => (byte)Math.Round((pixel.R + pixel.G + pixel.B) / 3.0),
            _ => throw new InvalidOperationException($"Unsupported source channel {mapping.SourceChannel}.")
        };
    }

    private sealed class DisposableImageLookup : IDisposable
    {
        private readonly Dictionary<Guid, Image<Rgba32>> _images = new();
        private readonly List<Image<Rgba32>> _owned = new();

        public DisposableImageLookup(IReadOnlyList<SourceImage> sources, int width, int height)
        {
            try
            {
                foreach (var source in sources)
                {
                    if (source.Width == width && source.Height == height)
                    {
                        _images[source.Id] = source.Pixels;
                        continue;
                    }

                    var clone = source.Pixels.Clone(ctx => ctx.Resize(width, height));
                    _owned.Add(clone);
                    _images[source.Id] = clone;
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public Image<Rgba32> this[Guid id] =>
            _images.TryGetValue(id, out var image)
                ? image
                : throw new InvalidOperationException("A mapped source image is no longer available.");

        public void Dispose()
        {
            foreach (var image in _owned)
            {
                image.Dispose();
            }
        }
    }
}
