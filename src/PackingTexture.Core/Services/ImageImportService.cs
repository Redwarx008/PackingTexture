using PackingTexture.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PackingTexture.Core.Services;

public static class ImageImportService
{
    public static async Task<SourceImage> ImportAsync(string path, CancellationToken cancellationToken)
    {
        var image = await Image.LoadAsync<Rgba32>(path, cancellationToken);
        var fileName = Path.GetFileName(path);
        var channels = DetectChannels(image);
        return new SourceImage(Guid.NewGuid(), fileName, image.Width, image.Height, channels, image);
    }

    private static SourceChannelSet DetectChannels(Image<Rgba32> image)
    {
        var hasAlpha = false;
        var isGray = true;

        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var pixel = image[x, y];
                hasAlpha |= pixel.A < 255;
                isGray &= pixel.R == pixel.G && pixel.G == pixel.B;
            }
        }

        if (hasAlpha)
        {
            return SourceChannelSet.Rgba;
        }

        return isGray ? SourceChannelSet.Gray : SourceChannelSet.Rgb;
    }
}
