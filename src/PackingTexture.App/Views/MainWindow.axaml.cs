using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PackingTexture.App.ViewModels;
using PackingTexture.Core.Models;

namespace PackingTexture.App.Views;

public partial class MainWindow : Window
{
    private static readonly DataFormat<string> SourceImageDragFormat =
        DataFormat.CreateInProcessFormat<string>("packingtexture-source-id");

    private sealed record ImportPathSplit(string[] AcceptedPaths, string[] RejectedMessages);

    private static readonly HashSet<string> SupportedImportExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".tga",
        ".tif",
        ".tiff"
    };

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnClosed(e);
    }

    private async void AddImages_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Add Images",
                AllowMultiple = true,
                FileTypeFilter =
                [
                    new FilePickerFileType("Images")
                    {
                        Patterns =
                        [
                            "*.png",
                            "*.jpg",
                            "*.jpeg",
                            "*.bmp",
                            "*.tga",
                            "*.tif",
                            "*.tiff"
                        ]
                    }
                ]
            });

            var paths = files
                .Select(file => file.TryGetLocalPath())
                .Where(path => path is not null)
                .Select(path => path!)
                .ToArray();

            if (paths.Length > 0)
            {
                await viewModel.AddImagesAsync(paths);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            viewModel.StatusText = $"Add images failed: {ex.Message}";
        }
    }

    private async void Export_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        try
        {
            var extension = viewModel.SelectedExportFormat == ExportFormat.Png ? "png" : "dds";
            var suggestedStartLocation = await TryGetSuggestedExportFolderAsync(viewModel);
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Packed Texture",
                DefaultExtension = extension,
                SuggestedFileName = viewModel.SuggestedExportFileName,
                SuggestedStartLocation = suggestedStartLocation,
                FileTypeChoices =
                [
                    new FilePickerFileType(extension.Equals("png", StringComparison.OrdinalIgnoreCase) ? "PNG Image" : "DDS Texture")
                    {
                        Patterns =
                        [
                            $"*.{extension}"
                        ]
                    }
                ]
            });

            var path = file?.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(path) && viewModel.ExportCommand.CanExecute(path))
            {
                await viewModel.ExportCommand.ExecuteAsync(path);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            viewModel.StatusText = $"Export failed: {ex.Message}";
        }
    }

    private async Task<IStorageFolder?> TryGetSuggestedExportFolderAsync(MainWindowViewModel viewModel)
    {
        if (string.IsNullOrWhiteSpace(viewModel.SuggestedExportDirectory))
        {
            return null;
        }

        try
        {
            return await StorageProvider.TryGetFolderFromPathAsync(new Uri(viewModel.SuggestedExportDirectory));
        }
        catch (UriFormatException)
        {
            return null;
        }
    }

    private async void Window_OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        try
        {
            var split = SplitImportPaths(e.DataTransfer.TryGetFiles()?
                .Select(file => file.TryGetLocalPath())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path!)
                .ToArray());

            if (split.AcceptedPaths.Length > 0)
            {
                await viewModel.AddImagesAsync(split.AcceptedPaths);
            }

            if (split.RejectedMessages.Length > 0)
            {
                viewModel.StatusText = AppendStatus(viewModel.StatusText, BuildRejectedDropStatus(split.RejectedMessages));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            viewModel.StatusText = $"Drop failed: {ex.Message}";
        }
    }

    private static string[] FilterSupportedImportPaths(IEnumerable<string>? paths) =>
        SplitImportPaths(paths).AcceptedPaths;

    private static ImportPathSplit SplitImportPaths(IEnumerable<string>? paths)
    {
        if (paths is null)
        {
            return new ImportPathSplit([], []);
        }

        var acceptedPaths = new List<string>();
        var rejectedMessages = new List<string>();

        foreach (var path in paths)
        {
            if (IsSupportedImportPath(path))
            {
                acceptedPaths.Add(path);
                continue;
            }

            var fileName = Path.GetFileName(path);
            rejectedMessages.Add($"Unsupported file type: {fileName}");
        }

        return new ImportPathSplit(acceptedPaths.ToArray(), rejectedMessages.ToArray());
    }

    private static bool IsSupportedImportPath(string path)
    {
        var extension = Path.GetExtension(path);
        return !string.IsNullOrWhiteSpace(extension) && SupportedImportExtensions.Contains(extension);
    }

    private static string BuildRejectedDropStatus(IReadOnlyList<string> rejectedMessages)
    {
        if (rejectedMessages.Count == 1)
        {
            return rejectedMessages[0];
        }

        var summary = string.Join("; ", rejectedMessages.Take(3));
        if (rejectedMessages.Count > 3)
        {
            summary = $"{summary}; +{rejectedMessages.Count - 3} more";
        }

        return summary;
    }

    private static string AppendStatus(string currentStatus, string nextStatus) =>
        string.IsNullOrWhiteSpace(currentStatus)
            ? nextStatus
            : $"{currentStatus} {nextStatus}";

    private async void SourceDragHandle_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not SourceImageViewModel source)
        {
            return;
        }

        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        try
        {
            var data = new DataTransfer();
            data.Add(DataTransferItem.Create(SourceImageDragFormat, source.Id.ToString()));
            await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
            e.Handled = true;
        }
        catch (Exception ex)
        {
            viewModel.StatusText = $"Drag failed: {ex.Message}";
        }
    }

    private void SourceCard_OnDragOver(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(SourceImageDragFormat))
        {
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void SourceCard_OnDrop(object? sender, DragEventArgs e)
    {
        if (sender is not Control control ||
            control.DataContext is not SourceImageViewModel target ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var sourceIdText = e.DataTransfer.TryGetValue(SourceImageDragFormat);
        if (!Guid.TryParse(sourceIdText, out var sourceId))
        {
            return;
        }

        var source = viewModel.SourceImages.FirstOrDefault(image => image.Id == sourceId);
        if (source is null)
        {
            return;
        }

        viewModel.MoveSourceBefore(source, target);
        e.Handled = true;
    }

    private void PreviewMode_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (button.Tag is null)
        {
            return;
        }

        if (Enum.TryParse<PreviewMode>(button.Tag.ToString(), true, out var mode))
        {
            viewModel.PreviewMode = mode;
        }
    }
}
