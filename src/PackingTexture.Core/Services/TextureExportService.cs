using BCnEncoder.Encoder;
using BCnEncoder.ImageSharp;
using BCnEncoder.Shared;
using PackingTexture.Core.Models;
using SixLabors.ImageSharp;

namespace PackingTexture.Core.Services;

public static class TextureExportService
{
    public static async Task ExportAsync(
        PackedImage image,
        string path,
        ExportSettings settings,
        CancellationToken cancellationToken)
    {
        ValidateExtension(path, settings.Format);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("Export path must include a directory.");
        }

        var tempPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                if (settings.Format == ExportFormat.Png)
                {
                    await image.Pixels.SaveAsPngAsync(stream, cancellationToken);
                }
                else
                {
                    var encoder = CreateEncoder(settings);
                    await encoder.EncodeToStreamAsync(image.Pixels, stream, cancellationToken);
                }
            }

            ReplaceTempFile(tempPath, fullPath);
        }
        catch
        {
            DeleteIfExists(tempPath);
            throw;
        }
    }

    private static void ReplaceTempFile(string tempPath, string destinationPath)
    {
        if (File.Exists(destinationPath))
        {
            File.Replace(tempPath, destinationPath, destinationBackupFileName: null);
            return;
        }

        File.Move(tempPath, destinationPath);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static BcEncoder CreateEncoder(ExportSettings settings) =>
        new()
        {
            OutputOptions =
            {
                FileFormat = OutputFileFormat.Dds,
                GenerateMipMaps = settings.GenerateMipMaps,
                Quality = CompressionQuality.Balanced,
                Format = ToCompressionFormat(settings.Format)
            }
        };

    private static void ValidateExtension(string path, ExportFormat format)
    {
        var extension = Path.GetExtension(path);
        if (format == ExportFormat.Png && !extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("PNG export requires a .png extension.");
        }

        if (format != ExportFormat.Png && !extension.Equals(".dds", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("DDS export requires a .dds extension.");
        }
    }

    private static CompressionFormat ToCompressionFormat(ExportFormat format) => format switch
    {
        ExportFormat.DdsBc1 => CompressionFormat.Bc1,
        ExportFormat.DdsBc3 => CompressionFormat.Bc3,
        ExportFormat.DdsBc4 => CompressionFormat.Bc4,
        ExportFormat.DdsBc5 => CompressionFormat.Bc5,
        ExportFormat.DdsBc7 => CompressionFormat.Bc7,
        _ => throw new InvalidOperationException($"Export format {format} is not a DDS BC format.")
    };
}
