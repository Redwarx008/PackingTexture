using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PackingTexture.App.ViewModels;
using PackingTexture.Core.Models;

namespace PackingTexture.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }

    private async void AddImages_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

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
                        "*.bmp",
                        "*.tga"
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

    private async void Export_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var extension = viewModel.SelectedExportFormat == ExportFormat.Png ? "png" : "dds";
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Packed Texture",
            DefaultExtension = extension,
            SuggestedFileName = $"packed.{extension}",
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

    private async void Window_OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var files = e.DataTransfer.TryGetFiles()?
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .ToArray();

        if (files is { Length: > 0 })
        {
            await viewModel.AddImagesAsync(files);
        }
    }

    private void PreviewMode_OnChecked(object? sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton radioButton || radioButton.IsChecked != true || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (radioButton.Tag is null)
        {
            return;
        }

        if (Enum.TryParse<PreviewMode>(radioButton.Tag.ToString(), true, out var mode))
        {
            viewModel.PreviewMode = mode;
        }
    }
}
