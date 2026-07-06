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

public sealed record SourceSelectionOption(string DisplayName, ChannelSourceKind SourceKind, SourceImageViewModel? SourceImage)
{
    public bool IsSource => SourceImage is not null;
}

public sealed partial class ChannelMappingViewModel : ObservableObject
{
    private readonly Action _refreshPreview;
    private IReadOnlyList<SourceSelectionOption> _sourceOptions = [];
    private SourceSelectionOption? _selectedSourceOption;
    private ChannelId? _selectedSourceChannel;

    private ChannelMapping mapping;

    public ChannelMappingViewModel(ChannelMapping mapping, Action refreshPreview)
    {
        this.mapping = mapping;
        _refreshPreview = refreshPreview;
    }

    public ChannelMapping Mapping
    {
        get => mapping;
        private set => SetProperty(ref mapping, value);
    }

    public ChannelId OutputChannel => Mapping.OutputChannel;

    public IReadOnlyList<SourceSelectionOption> SourceOptions => _sourceOptions;

    public IReadOnlyList<ChannelId> AvailableSourceChannels =>
        SelectedSourceOption?.SourceImage?.Source.AvailableChannels ?? [];

    public SourceSelectionOption? SelectedSourceOption
    {
        get => _selectedSourceOption;
        set
        {
            if (!SetProperty(ref _selectedSourceOption, value))
            {
                return;
            }

            if (_selectedSourceOption?.SourceImage is null)
            {
                _selectedSourceChannel = null;
                OnPropertyChanged(nameof(SelectedSourceChannel));
            }
            else if (_selectedSourceChannel is null ||
                     !_selectedSourceOption.SourceImage.Source.AvailableChannels.Contains(_selectedSourceChannel.Value))
            {
                _selectedSourceChannel = _selectedSourceOption.SourceImage.Source.AvailableChannels.First();
                OnPropertyChanged(nameof(SelectedSourceChannel));
            }

            OnPropertyChanged(nameof(AvailableSourceChannels));
            RebuildMapping();
        }
    }

    public ChannelId? SelectedSourceChannel
    {
        get => _selectedSourceChannel;
        set
        {
            if (!SetProperty(ref _selectedSourceChannel, value))
            {
                return;
            }

            RebuildMapping();
        }
    }

    public void AttachSourceImages(IReadOnlyList<SourceImageViewModel> sourceImages)
    {
        _sourceOptions = BuildSourceOptions(sourceImages);

        _selectedSourceOption = ResolveSelectedSourceOption();
        _selectedSourceChannel = ResolveSelectedSourceChannel();

        OnPropertyChanged(nameof(SourceOptions));
        OnPropertyChanged(nameof(SelectedSourceOption));
        OnPropertyChanged(nameof(AvailableSourceChannels));
        OnPropertyChanged(nameof(SelectedSourceChannel));
    }

    private IReadOnlyList<SourceSelectionOption> BuildSourceOptions(IReadOnlyList<SourceImageViewModel> sourceImages)
    {
        var options = new List<SourceSelectionOption>(sourceImages.Count + 2);
        options.AddRange(sourceImages.Select(source => new SourceSelectionOption(source.FileName, ChannelSourceKind.SourceChannel, source)));
        options.Add(new SourceSelectionOption("Zero", ChannelSourceKind.Zero, null));
        options.Add(new SourceSelectionOption("One", ChannelSourceKind.One, null));
        return options;
    }

    private SourceSelectionOption? ResolveSelectedSourceOption()
    {
        if (Mapping.SourceKind == ChannelSourceKind.SourceChannel && Mapping.SourceImageId is not null)
        {
            var match = _sourceOptions.FirstOrDefault(option => option.SourceImage?.Id == Mapping.SourceImageId.Value);
            if (match is not null)
            {
                return match;
            }
        }

        return _sourceOptions.FirstOrDefault(option => option.SourceKind == Mapping.SourceKind)
            ?? _sourceOptions.FirstOrDefault();
    }

    private ChannelId? ResolveSelectedSourceChannel()
    {
        if (_selectedSourceOption?.SourceImage is null)
        {
            return null;
        }

        if (Mapping.SourceKind == ChannelSourceKind.SourceChannel &&
            Mapping.SourceChannel is ChannelId channel &&
            _selectedSourceOption.SourceImage.Source.AvailableChannels.Contains(channel))
        {
            return channel;
        }

        return _selectedSourceOption.SourceImage.Source.AvailableChannels.First();
    }

    private void RebuildMapping()
    {
        if (_selectedSourceOption?.SourceImage is null)
        {
            Mapping = ChannelMapping.ForConstant(OutputChannel, _selectedSourceOption?.SourceKind ?? ChannelSourceKind.Zero, isAutomatic: false);
        }
        else
        {
            var channel = _selectedSourceChannel ?? _selectedSourceOption.SourceImage.Source.AvailableChannels.First();
            if (!_selectedSourceOption.SourceImage.Source.AvailableChannels.Contains(channel))
            {
                channel = _selectedSourceOption.SourceImage.Source.AvailableChannels.First();
                _selectedSourceChannel = channel;
                OnPropertyChanged(nameof(SelectedSourceChannel));
            }

            Mapping = ChannelMapping.ForSource(OutputChannel, _selectedSourceOption.SourceImage.Id, channel, isAutomatic: false);
        }

        OnPropertyChanged(nameof(OutputChannel));
        _refreshPreview();
    }
}

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly List<SourceImage> _sources = [];
    private PackedImage? _packedImage;
    private AvaloniaBitmap? _previewBitmap;

    public AvaloniaBitmap? PreviewBitmap
    {
        get => _previewBitmap;
        private set
        {
            if (ReferenceEquals(_previewBitmap, value))
            {
                return;
            }

            var previous = _previewBitmap;
            if (SetProperty(ref _previewBitmap, value))
            {
                previous?.Dispose();
            }
        }
    }

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
    }

    public void ApplyAutomaticMappings()
    {
        Mappings.Clear();
        foreach (var mapping in ChannelPackingService.CreateDefaultMappings(_sources))
        {
            var viewModel = new ChannelMappingViewModel(mapping, RefreshPreview);
            viewModel.AttachSourceImages(SourceImages.ToArray());
            Mappings.Add(viewModel);
        }

        RefreshPreview();
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
