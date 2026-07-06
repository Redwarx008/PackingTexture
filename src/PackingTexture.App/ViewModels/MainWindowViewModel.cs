using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PackingTexture.Core.Models;
using PackingTexture.Core.Services;
using SixLabors.ImageSharp;

namespace PackingTexture.App.ViewModels;

public enum PreviewMode
{
    Rgba,
    R,
    G,
    B,
    A
}

public sealed partial class SourceImageViewModel : ObservableObject
{
    public SourceImageViewModel(SourceImage source)
    {
        Source = source;
    }

    public SourceImage Source { get; }

    public Guid Id => Source.Id;

    public string FileName => Source.FileName;

    public string Dimensions => $"{Source.Width} x {Source.Height}";

    public string ChannelSummary => Source.Channels.ToString();
}

public sealed partial class ChannelMappingViewModel : ObservableObject
{
    [ObservableProperty]
    private ChannelMapping mapping;

    public ChannelMappingViewModel(ChannelMapping mapping)
    {
        this.mapping = mapping;
    }

    public ChannelId OutputChannel => Mapping.OutputChannel;

    partial void OnMappingChanged(ChannelMapping value)
    {
        OnPropertyChanged(nameof(OutputChannel));
    }
}

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly List<SourceImage> _sources = [];
    private PackedImage? _packedImage;

    [ObservableProperty]
    private AvaloniaBitmap? previewBitmap;

    [ObservableProperty]
    private PreviewMode previewMode;

    [ObservableProperty]
    private ExportFormat selectedExportFormat = ExportFormat.DdsBc7;

    [ObservableProperty]
    private bool generateMipMaps = true;

    [ObservableProperty]
    private bool flipY;

    [ObservableProperty]
    private string statusText = "Add images to begin.";

    public ObservableCollection<SourceImageViewModel> SourceImages { get; } = [];

    public ObservableCollection<ChannelMappingViewModel> Mappings { get; } = [];

    public IReadOnlyList<ExportFormat> ExportFormats { get; } =
    [
        ExportFormat.Png,
        ExportFormat.DdsBc1,
        ExportFormat.DdsBc3,
        ExportFormat.DdsBc4,
        ExportFormat.DdsBc5,
        ExportFormat.DdsBc7
    ];

    public async Task AddImagesAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
    {
        foreach (var path in paths)
        {
            var source = await ImageImportService.ImportAsync(path, cancellationToken);
            _sources.Add(source);
            SourceImages.Add(new SourceImageViewModel(source));
        }

        ApplyAutomaticMappings();
        RefreshPreview();
    }

    public void ApplyAutomaticMappings()
    {
        Mappings.Clear();
        foreach (var mapping in ChannelPackingService.CreateDefaultMappings(_sources))
        {
            Mappings.Add(new ChannelMappingViewModel(mapping));
        }
    }

    partial void OnFlipYChanged(bool value) => RefreshPreview();

    partial void OnPreviewModeChanged(PreviewMode value) => RefreshPreview();

    [RelayCommand]
    private async Task ExportAsync(string path)
    {
        if (_packedImage is null)
        {
            StatusText = "Nothing to export.";
            return;
        }

        await TextureExportService.ExportAsync(
            _packedImage,
            path,
            new ExportSettings(SelectedExportFormat, GenerateMipMaps),
            CancellationToken.None);

        StatusText = $"Exported {Path.GetFileName(path)}";
    }

    private void RefreshPreview()
    {
        _packedImage?.Dispose();
        _packedImage = null;

        if (_sources.Count == 0 || Mappings.Count != 4)
        {
            PreviewBitmap = null;
            return;
        }

        _packedImage = ChannelPackingService.Pack(_sources, Mappings.Select(m => m.Mapping).ToList(), FlipY);
        StatusText = _packedImage.HadResizedSources
            ? "Some sources were resized to match the first image."
            : "Ready.";

        using var previewImage = PreviewImageFactory.Create(_packedImage, PreviewMode);
        using var stream = new MemoryStream();
        previewImage.SaveAsPng(stream);
        stream.Position = 0;
        PreviewBitmap = new AvaloniaBitmap(stream);
    }
}
