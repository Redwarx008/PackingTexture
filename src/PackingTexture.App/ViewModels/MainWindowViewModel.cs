using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PackingTexture.Core.Models;
using PackingTexture.Core.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace PackingTexture.App.ViewModels;

public enum PreviewMode
{
    Rgba,
    R,
    G,
    B,
    A
}

public sealed partial class SourceImageViewModel : ObservableObject, IDisposable
{
    public SourceImageViewModel(SourceImage source)
    {
        Source = source;
        ThumbnailBitmap = TryCreateThumbnail(source);
    }

    public SourceImage Source { get; }

    public AvaloniaBitmap? ThumbnailBitmap { get; }

    public Guid Id => Source.Id;

    public string FileName => Source.FileName;

    public string Dimensions => $"{Source.Width} x {Source.Height}";

    public string SourceSummary => $"{Dimensions} - {ChannelSummary}";

    public string ChannelSummary => Source.Channels switch
    {
        SourceChannelSet.Gray => "Gray",
        SourceChannelSet.Rgb => "RGB",
        SourceChannelSet.Rgba => "RGBA",
        _ => Source.Channels.ToString()
    };

    private static AvaloniaBitmap? TryCreateThumbnail(SourceImage source)
    {
        try
        {
            using var thumbnail = source.Pixels.Clone(context => context.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Crop,
                Size = new Size(72, 72)
            }));
            using var stream = new MemoryStream();
            thumbnail.SaveAsPng(stream);
            stream.Position = 0;
            return new AvaloniaBitmap(stream);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public void Dispose() => ThumbnailBitmap?.Dispose();
}

public sealed record ExportFormatOption(ExportFormat Format, string DisplayName)
{
    public override string ToString() => DisplayName;
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

    [ObservableProperty]
    private bool isActiveForExport = true;

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

    public IBrush OutputChannelBrush => OutputChannel switch
    {
        ChannelId.R => Brushes.Firebrick,
        ChannelId.G => Brushes.ForestGreen,
        ChannelId.B => Brushes.RoyalBlue,
        ChannelId.A => Brushes.SlateGray,
        _ => Brushes.Black
    };

    public double RowOpacity => IsActiveForExport ? 1.0 : 0.38;

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

    public void SetActiveForExport(bool isActive) => IsActiveForExport = isActive;

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

    partial void OnIsActiveForExportChanged(bool value) => OnPropertyChanged(nameof(RowOpacity));
}

public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private const int PreviewMaxDimension = 1024;

    private readonly Func<string, CancellationToken, Task<SourceImage>> _importAsync;
    private readonly Func<PackedImage, string, ExportSettings, CancellationToken, Task> _exportAsync;
    private readonly List<SourceImage> _sources = [];
    private Guid? _primarySourceId;
    private PackedImage? _previewPackedImage;
    private AvaloniaBitmap? _previewBitmap;
    private string? _suggestedExportDirectory;
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

    [ObservableProperty]
    private string outputSizeText = "Output: -";

    public ObservableCollection<SourceImageViewModel> SourceImages { get; } = [];

    public ObservableCollection<ChannelMappingViewModel> Mappings { get; } = [];

    public AvaloniaBitmap? CheckerboardBitmap { get; } = TryCreateCheckerboardBitmap();

    public IReadOnlyList<ExportFormatOption> ExportFormatOptions { get; } =
    [
        new(ExportFormat.Png, "PNG"),
        new(ExportFormat.DdsBc1, "DDS BC1"),
        new(ExportFormat.DdsBc3, "DDS BC3"),
        new(ExportFormat.DdsBc4, "DDS BC4"),
        new(ExportFormat.DdsBc5, "DDS BC5"),
        new(ExportFormat.DdsBc7, "DDS BC7")
    ];

    public ExportFormatOption? SelectedExportFormatOption
    {
        get => ExportFormatOptions.First(option => option.Format == SelectedExportFormat);
        set
        {
            if (value is null || value.Format == SelectedExportFormat)
            {
                return;
            }

            SelectedExportFormat = value.Format;
            OnPropertyChanged();
        }
    }

    public string SuggestedExportFileName => $"{InferExportBaseName()}.{GetExportExtension()}";

    public string? SuggestedExportDirectory => _suggestedExportDirectory;

    public IReadOnlyList<ChannelId> ActiveOutputChannels => GetActiveOutputChannels(SelectedExportFormat);

    public bool HasStatusMessage =>
        !string.IsNullOrWhiteSpace(StatusText) &&
        !StatusText.Equals("Ready.", StringComparison.OrdinalIgnoreCase) &&
        !StatusText.Equals("Add images to begin.", StringComparison.OrdinalIgnoreCase);

    public async Task AddImagesAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
    {
        var failures = new List<string>();

        foreach (var path in paths)
        {
            try
            {
                var source = await _importAsync(path, cancellationToken);
                _primarySourceId ??= source.Id;
                SetSuggestedExportDirectoryIfNeeded(path);
                _sources.Add(source);
                SourceImages.Add(new SourceImageViewModel(source));
                OnPropertyChanged(nameof(SuggestedExportFileName));
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

    public void MoveSourceBefore(SourceImageViewModel source, SourceImageViewModel target)
    {
        var sourceIndex = SourceImages.IndexOf(source);
        var targetIndex = SourceImages.IndexOf(target);
        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
        {
            return;
        }

        if (sourceIndex < targetIndex)
        {
            targetIndex--;
        }

        SourceImages.Move(sourceIndex, targetIndex);

        var core = _sources[sourceIndex];
        _sources.RemoveAt(sourceIndex);
        _sources.Insert(targetIndex, core);

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
            viewModel.SetActiveForExport(IsOutputChannelActive(mapping.OutputChannel));
            Mappings.Add(viewModel);
        }

        RefreshPreview();
    }

    partial void OnFlipYChanged(bool value) => RefreshPreview();

    partial void OnPreviewModeChanged(PreviewMode value) => RenderPreviewBitmap();

    partial void OnSelectedExportFormatChanged(ExportFormat value)
    {
        OnPropertyChanged(nameof(SelectedExportFormatOption));
        OnPropertyChanged(nameof(SuggestedExportFileName));
        OnPropertyChanged(nameof(ActiveOutputChannels));
        UpdateMappingActivity();
        RefreshPreview();
    }

    partial void OnStatusTextChanged(string value) => OnPropertyChanged(nameof(HasStatusMessage));

    [RelayCommand]
    private void SetPreviewMode(PreviewMode mode) => PreviewMode = mode;

    [RelayCommand]
    private async Task ExportAsync(string path)
    {
        if (_sources.Count == 0 || Mappings.Count != 4)
        {
            StatusText = "Nothing to export.";
            return;
        }

        try
        {
            using var packedImage = ChannelPackingService.Pack(GetPackingSources(), GetPreviewMappings(), FlipY);

            await _exportAsync(
                packedImage,
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
        _previewPackedImage?.Dispose();
        _previewPackedImage = null;

        if (_sources.Count == 0 || Mappings.Count != 4)
        {
            OutputSizeText = "Output: -";
            PreviewBitmap = null;
            return;
        }

        try
        {
            using var previewSources = CreatePreviewSourceSet();
            _previewPackedImage = ChannelPackingService.Pack(previewSources.Sources, GetPreviewMappings(), FlipY);
        }
        catch (Exception ex)
        {
            _previewPackedImage?.Dispose();
            _previewPackedImage = null;
            OutputSizeText = "Output: -";
            PreviewBitmap = null;
            StatusText = ex.Message;
            return;
        }

        var outputSources = GetPackingSources();
        OutputSizeText = $"Output: {outputSources[0].Width} x {outputSources[0].Height}";
        StatusText = HasResizedOriginalSources(outputSources)
            ? "Some sources were resized to match the first image."
            : "Ready.";

        RenderPreviewBitmap();
    }

    private void RenderPreviewBitmap()
    {
        if (_previewPackedImage is null)
        {
            PreviewBitmap = null;
            return;
        }

        try
        {
            using var previewImage = PreviewImageFactory.Create(_previewPackedImage, PreviewMode);
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

    private PreviewSourceSet CreatePreviewSourceSet()
    {
        var packingSources = GetPackingSources();
        var (previewWidth, previewHeight) = GetPreviewSize(packingSources[0].Width, packingSources[0].Height);
        var previewSources = new List<SourceImage>(packingSources.Count);
        var ownedImages = new List<Image<Rgba32>>(packingSources.Count);

        try
        {
            foreach (var source in packingSources)
            {
                var previewPixels = source.Pixels.Clone(context => context.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Stretch,
                    Size = new Size(previewWidth, previewHeight)
                }));

                ownedImages.Add(previewPixels);
                previewSources.Add(source with
                {
                    Width = previewWidth,
                    Height = previewHeight,
                    Pixels = previewPixels
                });
            }

            return new PreviewSourceSet(previewSources, ownedImages);
        }
        catch
        {
            foreach (var image in ownedImages)
            {
                image.Dispose();
            }

            throw;
        }
    }

    private static (int Width, int Height) GetPreviewSize(int width, int height)
    {
        var longestSide = Math.Max(width, height);
        if (longestSide <= PreviewMaxDimension)
        {
            return (width, height);
        }

        var scale = PreviewMaxDimension / (double)longestSide;
        return (
            Math.Max(1, (int)Math.Round(width * scale)),
            Math.Max(1, (int)Math.Round(height * scale)));
    }

    private static bool HasResizedOriginalSources(IReadOnlyList<SourceImage> sources)
    {
        var width = sources[0].Width;
        var height = sources[0].Height;
        return sources.Any(source => source.Width != width || source.Height != height);
    }

    private IReadOnlyList<ChannelMapping> GetPreviewMappings() =>
        ExportFormatChannelPolicy.MaskInactiveMappings(
            SelectedExportFormat,
            Mappings.Select(mapping => mapping.Mapping).ToArray());

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

    private string InferExportBaseName()
    {
        var names = _sources
            .Select(source => Path.GetFileNameWithoutExtension(source.FileName))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();

        if (names.Length == 0)
        {
            return "packed";
        }

        if (names.Length == 1)
        {
            return names[0];
        }

        var prefix = FindCommonPrefix(names).TrimEnd(' ', '_', '-', '.');
        return prefix.Length >= 3 ? prefix : names[0];
    }

    private string GetExportExtension() => SelectedExportFormat == ExportFormat.Png ? "png" : "dds";

    private void UpdateMappingActivity()
    {
        foreach (var mapping in Mappings)
        {
            mapping.SetActiveForExport(IsOutputChannelActive(mapping.OutputChannel));
        }
    }

    private bool IsOutputChannelActive(ChannelId channel) =>
        ExportFormatChannelPolicy.IsOutputChannelActive(SelectedExportFormat, channel);

    private static IReadOnlyList<ChannelId> GetActiveOutputChannels(ExportFormat format) =>
        ExportFormatChannelPolicy.GetActiveOutputChannels(format);

    private void SetSuggestedExportDirectoryIfNeeded(string path)
    {
        if (!string.IsNullOrWhiteSpace(_suggestedExportDirectory))
        {
            return;
        }

        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        _suggestedExportDirectory = Path.GetFullPath(directory);
        OnPropertyChanged(nameof(SuggestedExportDirectory));
    }

    private static string FindCommonPrefix(IReadOnlyList<string> names)
    {
        var first = names[0];
        var length = first.Length;

        for (var nameIndex = 1; nameIndex < names.Count; nameIndex++)
        {
            length = Math.Min(length, names[nameIndex].Length);
            for (var charIndex = 0; charIndex < length; charIndex++)
            {
                if (first[charIndex] != names[nameIndex][charIndex])
                {
                    length = charIndex;
                    break;
                }
            }
        }

        return first[..length];
    }

    private static AvaloniaBitmap? TryCreateCheckerboardBitmap()
    {
        try
        {
            using var image = new Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(768, 512);
            var light = new SixLabors.ImageSharp.PixelFormats.Rgba32(245, 248, 252, 255);
            var dark = new SixLabors.ImageSharp.PixelFormats.Rgba32(225, 232, 242, 255);
            const int tileSize = 16;

            for (var y = 0; y < image.Height; y++)
            {
                for (var x = 0; x < image.Width; x++)
                {
                    image[x, y] = ((x / tileSize) + (y / tileSize)) % 2 == 0 ? light : dark;
                }
            }

            using var stream = new MemoryStream();
            image.SaveAsPng(stream);
            stream.Position = 0;
            return new AvaloniaBitmap(stream);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _previewPackedImage?.Dispose();
        _previewPackedImage = null;

        PreviewBitmap = null;
        CheckerboardBitmap?.Dispose();

        foreach (var sourceImage in SourceImages)
        {
            sourceImage.Dispose();
        }

        foreach (var source in _sources)
        {
            source.Pixels.Dispose();
        }

        _sources.Clear();
        SourceImages.Clear();
        Mappings.Clear();
        _primarySourceId = null;
    }

    private sealed class PreviewSourceSet : IDisposable
    {
        private readonly IReadOnlyList<Image<Rgba32>> _ownedImages;

        public PreviewSourceSet(IReadOnlyList<SourceImage> sources, IReadOnlyList<Image<Rgba32>> ownedImages)
        {
            Sources = sources;
            _ownedImages = ownedImages;
        }

        public IReadOnlyList<SourceImage> Sources { get; }

        public void Dispose()
        {
            foreach (var image in _ownedImages)
            {
                image.Dispose();
            }
        }
    }
}
