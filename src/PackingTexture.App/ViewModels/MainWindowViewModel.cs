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

public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly Func<string, CancellationToken, Task<SourceImage>> _importAsync;
    private readonly Func<PackedImage, string, ExportSettings, CancellationToken, Task> _exportAsync;
    private readonly List<SourceImage> _sources = [];
    private Guid? _primarySourceId;
    private PackedImage? _packedImage;
    private AvaloniaBitmap? _previewBitmap;
    private bool _disposed;

    public MainWindowViewModel()
        : this(ImageImportService.ImportAsync, TextureExportService.ExportAsync)
    {
    }

    public MainWindowViewModel(
        Func<string, CancellationToken, Task<SourceImage>> importAsync,
        Func<PackedImage, string, ExportSettings, CancellationToken, Task> exportAsync)
    {
        _importAsync = importAsync ?? throw new ArgumentNullException(nameof(importAsync));
        _exportAsync = exportAsync ?? throw new ArgumentNullException(nameof(exportAsync));
    }

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
        var failures = new List<string>();

        foreach (var path in paths)
        {
            try
            {
                var source = await _importAsync(path, cancellationToken);
                _primarySourceId ??= source.Id;
                _sources.Add(source);
                SourceImages.Add(new SourceImageViewModel(source));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failures.Add($"{Path.GetFileName(path)} ({ex.Message})");
            }
        }

        if (_sources.Count > 0)
        {
            ApplyAutomaticMappings();
        }

        if (failures.Count > 0)
        {
            StatusText = AppendStatus(StatusText, BuildFailureSummary("Failed to import", "file", failures));
        }
    }

    [RelayCommand]
    private void MoveSourceUp(SourceImageViewModel source)
    {
        var index = SourceImages.IndexOf(source);
        if (index <= 0)
        {
            return;
        }

        SourceImages.Move(index, index - 1);
        var core = _sources[index];
        _sources.RemoveAt(index);
        _sources.Insert(index - 1, core);
        ApplyAutomaticMappings();
    }

    [RelayCommand]
    private void MoveSourceDown(SourceImageViewModel source)
    {
        var index = SourceImages.IndexOf(source);
        if (index < 0 || index >= SourceImages.Count - 1)
        {
            return;
        }

        SourceImages.Move(index, index + 1);
        var core = _sources[index];
        _sources.RemoveAt(index);
        _sources.Insert(index + 1, core);
        ApplyAutomaticMappings();
    }

    public void ApplyAutomaticMappings()
    {
        var sourceImages = SourceImages.ToArray();
        var mappings = ChannelPackingService.RebuildMappingsForSourceOrder(_sources, Mappings.Select(m => m.Mapping).ToArray());

        Mappings.Clear();
        foreach (var mapping in mappings)
        {
            var viewModel = new ChannelMappingViewModel(mapping, RefreshPreview);
            viewModel.AttachSourceImages(sourceImages);
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

        try
        {
            await _exportAsync(
                _packedImage,
                path,
                new ExportSettings(SelectedExportFormat, GenerateMipMaps),
                CancellationToken.None);

            StatusText = $"Exported {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Export failed: {ex.Message}";
        }
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

        try
        {
            _packedImage = ChannelPackingService.Pack(GetPackingSources(), Mappings.Select(m => m.Mapping).ToList(), FlipY);
        }
        catch (Exception ex)
        {
            _packedImage?.Dispose();
            _packedImage = null;
            PreviewBitmap = null;
            StatusText = ex.Message;
            return;
        }

        StatusText = _packedImage.HadResizedSources
            ? "Some sources were resized to match the first image."
            : "Ready.";

        try
        {
            using var previewImage = PreviewImageFactory.Create(_packedImage, PreviewMode);
            using var stream = new MemoryStream();
            previewImage.SaveAsPng(stream);
            stream.Position = 0;
            PreviewBitmap = new AvaloniaBitmap(stream);
        }
        catch (Exception ex)
        {
            PreviewBitmap = null;
            StatusText = $"Preview failed: {ex.Message}";
        }
    }

    private IReadOnlyList<SourceImage> GetPackingSources()
    {
        if (_primarySourceId is null)
        {
            return _sources;
        }

        var primaryIndex = _sources.FindIndex(source => source.Id == _primarySourceId.Value);
        if (primaryIndex <= 0)
        {
            return _sources;
        }

        var packingSources = new List<SourceImage>(_sources.Count)
        {
            _sources[primaryIndex]
        };

        for (var index = 0; index < _sources.Count; index++)
        {
            if (index == primaryIndex)
            {
                continue;
            }

            packingSources.Add(_sources[index]);
        }

        return packingSources;
    }

    private static string BuildFailureSummary(string action, string singularNoun, IReadOnlyList<string> failures)
    {
        var noun = failures.Count == 1 ? singularNoun : $"{singularNoun}s";
        var summary = string.Join("; ", failures.Take(3));
        if (failures.Count > 3)
        {
            summary = $"{summary}; +{failures.Count - 3} more";
        }

        return $"{action} {failures.Count} {noun}: {summary}";
    }

    private static string AppendStatus(string currentStatus, string nextStatus) =>
        string.IsNullOrWhiteSpace(currentStatus)
            ? nextStatus
            : $"{currentStatus} {nextStatus}";

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _packedImage?.Dispose();
        _packedImage = null;

        PreviewBitmap = null;

        foreach (var source in _sources)
        {
            source.Pixels.Dispose();
        }

        _sources.Clear();
        SourceImages.Clear();
        Mappings.Clear();
        _primarySourceId = null;
    }
}
