using System.Reflection;
using PackingTexture.App.ViewModels;
using AppWindow = PackingTexture.App.Views.MainWindow;
using PackingTexture.Core.Models;
using PackingTexture.Core.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PackingTexture.Core.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void ReorderingSources_PreservesPrimaryOutputSize_WhileRecomputingAutomaticMappings()
    {
        var primaryId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var secondaryId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        using var primary = new Image<Rgba32>(3, 5);
        using var secondary = new Image<Rgba32>(1, 1);
        primary[0, 0] = new Rgba32(10, 20, 30, 255);
        secondary[0, 0] = new Rgba32(80, 80, 80, 255);
        using var primaryPixels = primary.Clone();
        using var secondaryPixels = secondary.Clone();

        var primarySource = new SourceImage(primaryId, "Primary.png", 3, 5, SourceChannelSet.Rgb, primaryPixels);
        var secondarySource = new SourceImage(secondaryId, "Secondary.png", 1, 1, SourceChannelSet.Gray, secondaryPixels);
        var viewModel = new MainWindowViewModel();

        SetPrivateField(viewModel, "_sources", new List<SourceImage> { secondarySource, primarySource });
        SetPrivateField(viewModel, "_primarySourceId", primaryId);

        var packingSources = (IReadOnlyList<SourceImage>)InvokePrivateMethod(viewModel, "GetPackingSources")!;

        Assert.Equal(primaryId, packingSources[0].Id);
        Assert.Equal(secondaryId, packingSources[1].Id);

        var mappings = new[]
        {
            ChannelMapping.ForSource(ChannelId.R, secondaryId, ChannelId.Gray),
            ChannelMapping.ForSource(ChannelId.G, primaryId, ChannelId.R),
            ChannelMapping.ForConstant(ChannelId.B, ChannelSourceKind.Zero),
            ChannelMapping.ForConstant(ChannelId.A, ChannelSourceKind.One)
        };

        using var packed = ChannelPackingService.Pack(packingSources, mappings, flipGreen: false);

        Assert.Equal(3, packed.Width);
        Assert.Equal(5, packed.Height);
        Assert.Equal((byte)80, packed.Pixels[0, 0].R);
        Assert.Equal((byte)10, packed.Pixels[0, 0].G);
    }

    [Fact]
    public void FilterSupportedImportPaths_IgnoresUnsupportedFiles()
    {
        var method = typeof(AppWindow)
            .GetMethod("FilterSupportedImportPaths", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var result = (string[])method!.Invoke(null, [new[]
        {
            "C:/tmp/one.png",
            "C:/tmp/two.txt",
            "C:/tmp/three.JPEG",
            "C:/tmp/four.gif"
        }])!;

        Assert.Equal(["C:/tmp/one.png", "C:/tmp/three.JPEG"], result);
    }

    [Fact]
    public void SplitImportPaths_ReturnsAcceptedPaths_AndRejectedMessages()
    {
        var method = typeof(AppWindow)
            .GetMethod("SplitImportPaths", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var result = method!.Invoke(null, [new[]
        {
            "C:/tmp/one.png",
            "C:/tmp/two.dds",
            "C:/tmp/three.JPEG",
            "C:/tmp/four.gif"
        }]);

        Assert.NotNull(result);

        var acceptedPaths = (string[]?)result!.GetType().GetProperty("AcceptedPaths", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(result);
        var rejectedMessages = (string[]?)result.GetType().GetProperty("RejectedMessages", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(result);

        Assert.Equal(["C:/tmp/one.png", "C:/tmp/three.JPEG"], acceptedPaths);
        Assert.Equal(
        [
            "Unsupported file type: two.dds",
            "Unsupported file type: four.gif"
        ], rejectedMessages);
    }

    [Fact]
    public async Task AddImagesAsync_ContinuesPastFailedImports_AndReportsInlineStatus()
    {
        var goodSource = CreateSourceImage(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "good.png",
            2,
            2,
            SourceChannelSet.Rgba,
            new Rgba32(10, 20, 30, 255));
        using var goodSourceScope = goodSource;
        var viewModel = new MainWindowViewModel(
            importAsync: (path, _) => path.EndsWith("bad.png", StringComparison.OrdinalIgnoreCase)
                ? Task.FromException<SourceImage>(new InvalidOperationException("Unsupported image format."))
                : Task.FromResult(goodSource.Source),
            exportAsync: (_, _, _, _) => Task.CompletedTask);

        await viewModel.AddImagesAsync(["C:/tmp/good.png", "C:/tmp/bad.png"]);

        Assert.Single(viewModel.SourceImages);
        Assert.Equal(4, viewModel.Mappings.Count);
        Assert.Contains("Failed to import 1 file", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Unsupported image format", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddImagesAsync_UpdatesOutputSizeText()
    {
        var goodSource = CreateSourceImage(
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            "size.png",
            7,
            9,
            SourceChannelSet.Rgba,
            new Rgba32(10, 20, 30, 255));
        using var goodSourceScope = goodSource;
        var viewModel = new MainWindowViewModel(
            importAsync: (_, _) => Task.FromResult(goodSource.Source),
            exportAsync: (_, _, _, _) => Task.CompletedTask);

        await viewModel.AddImagesAsync(["C:/tmp/size.png"]);

        Assert.Equal("Output: 7 x 9", viewModel.OutputSizeText);
    }

    [Fact]
    public async Task ClearCommand_RemovesSourcesMappingsAndPreview()
    {
        var sourceScope = CreateSourceImage(
            Guid.Parse("13131313-1313-1313-1313-131313131313"),
            "clear.png",
            2,
            2,
            SourceChannelSet.Rgba,
            new Rgba32(10, 20, 30, 40));
        var source = sourceScope.Source;
        sourceScope.Detach();
        using var viewModel = new MainWindowViewModel(
            importAsync: (_, _) => Task.FromResult(source),
            exportAsync: (_, _, _, _) => Task.CompletedTask);

        await viewModel.AddImagesAsync(["C:/tmp/clear.png"]);
        Assert.NotEmpty(viewModel.SourceImages);
        Assert.NotEmpty(viewModel.Mappings);
        Assert.NotNull(GetPrivateField(viewModel, "_previewPackedImage"));

        viewModel.ClearCommand.Execute(null);

        Assert.Empty(viewModel.SourceImages);
        Assert.Empty(viewModel.Mappings);
        Assert.Null(viewModel.PreviewBitmap);
        Assert.Equal("Output: -", viewModel.OutputSizeText);
        Assert.Equal("Add images to begin.", viewModel.StatusText);
        Assert.Null(GetPrivateField(viewModel, "_primarySourceId"));
        Assert.Null(GetPrivateField(viewModel, "_previewPackedImage"));
        Assert.Throws<ObjectDisposedException>(() => source.Pixels.Clone());
    }

    [Fact]
    public async Task MoveSourceBefore_ReordersVisibleSources_ForDragDrop()
    {
        var first = CreateSourceImage(
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            "first.png",
            2,
            2,
            SourceChannelSet.Rgb,
            new Rgba32(10, 20, 30, 255));
        var second = CreateSourceImage(
            Guid.Parse("66666666-6666-6666-6666-666666666666"),
            "second.png",
            2,
            2,
            SourceChannelSet.Gray,
            new Rgba32(40, 40, 40, 255));
        var third = CreateSourceImage(
            Guid.Parse("77777777-7777-7777-7777-777777777777"),
            "third.png",
            2,
            2,
            SourceChannelSet.Gray,
            new Rgba32(70, 70, 70, 255));
        first.Detach();
        second.Detach();
        third.Detach();

        using var viewModel = new MainWindowViewModel(
            importAsync: (path, _) => Task.FromResult(path switch
            {
                "first" => first.Source,
                "second" => second.Source,
                _ => third.Source
            }),
            exportAsync: (_, _, _, _) => Task.CompletedTask);

        await viewModel.AddImagesAsync(["first", "second", "third"]);

        viewModel.MoveSourceBefore(viewModel.SourceImages[2], viewModel.SourceImages[0]);

        Assert.Equal(["third.png", "first.png", "second.png"], viewModel.SourceImages.Select(source => source.FileName).ToArray());
        var coreSources = (IReadOnlyList<SourceImage>)GetPrivateField(viewModel, "_sources")!;
        Assert.Equal(["third.png", "first.png", "second.png"], coreSources.Select(source => source.FileName).ToArray());
    }

    [Fact]
    public void ExportFormatOptions_UseReadableLabels()
    {
        var viewModel = new MainWindowViewModel();

        Assert.Contains(viewModel.ExportFormatOptions, option => option.DisplayName == "DDS BC7");
        Assert.Equal("DDS BC7", viewModel.SelectedExportFormatOption?.DisplayName);
    }

    [Theory]
    [InlineData(ExportFormat.Png, "R,G,B,A")]
    [InlineData(ExportFormat.DdsBc1, "R,G,B")]
    [InlineData(ExportFormat.DdsBc3, "R,G,B,A")]
    [InlineData(ExportFormat.DdsBc4, "R")]
    [InlineData(ExportFormat.DdsBc5, "R,G")]
    [InlineData(ExportFormat.DdsBc7, "R,G,B,A")]
    public void SelectedExportFormat_ControlsActiveOutputChannels(ExportFormat format, string expectedChannels)
    {
        var viewModel = new MainWindowViewModel
        {
            SelectedExportFormat = format
        };

        var expected = expectedChannels.Split(',').Select(Enum.Parse<ChannelId>).ToArray();

        Assert.Equal(expected, viewModel.ActiveOutputChannels);
    }

    [Fact]
    public async Task SelectedExportFormat_DisablesInactiveMappingRows()
    {
        var source = CreateSourceImage(
            Guid.Parse("67676767-6767-6767-6767-676767676767"),
            "packed.png",
            2,
            2,
            SourceChannelSet.Rgba,
            new Rgba32(10, 20, 30, 255));
        source.Detach();
        using var viewModel = new MainWindowViewModel(
            importAsync: (_, _) => Task.FromResult(source.Source),
            exportAsync: (_, _, _, _) => Task.CompletedTask);

        await viewModel.AddImagesAsync(["packed"]);
        viewModel.SelectedExportFormat = ExportFormat.DdsBc5;

        Assert.True(viewModel.Mappings.Single(mapping => mapping.OutputChannel == ChannelId.R).IsActiveForExport);
        Assert.True(viewModel.Mappings.Single(mapping => mapping.OutputChannel == ChannelId.G).IsActiveForExport);
        Assert.False(viewModel.Mappings.Single(mapping => mapping.OutputChannel == ChannelId.B).IsActiveForExport);
        Assert.False(viewModel.Mappings.Single(mapping => mapping.OutputChannel == ChannelId.A).IsActiveForExport);
    }

    [Fact]
    public async Task SelectedExportFormat_MasksInactiveChannelsInPackedPreview()
    {
        var source = CreateSourceImage(
            Guid.Parse("78787878-7878-7878-7878-787878787878"),
            "packed.png",
            1,
            1,
            SourceChannelSet.Rgba,
            new Rgba32(10, 20, 30, 40));
        source.Detach();
        using var viewModel = new MainWindowViewModel(
            importAsync: (_, _) => Task.FromResult(source.Source),
            exportAsync: (_, _, _, _) => Task.CompletedTask);

        await viewModel.AddImagesAsync(["packed"]);
        viewModel.SelectedExportFormat = ExportFormat.DdsBc5;

        var packedImage = (PackedImage?)GetPrivateField(viewModel, "_previewPackedImage");

        Assert.NotNull(packedImage);
        var pixel = packedImage!.Pixels[0, 0];
        Assert.Equal((byte)10, pixel.R);
        Assert.Equal((byte)20, pixel.G);
        Assert.Equal((byte)0, pixel.B);
        Assert.Equal((byte)255, pixel.A);
    }

    [Fact]
    public async Task AddImagesAsync_DownscalesPackedPreviewCache_ForLargeSources()
    {
        var source = CreateSourceImage(
            Guid.Parse("89898989-8989-8989-8989-898989898989"),
            "large.png",
            2048,
            1024,
            SourceChannelSet.Rgba,
            new Rgba32(10, 20, 30, 255));
        source.Detach();
        using var viewModel = new MainWindowViewModel(
            importAsync: (_, _) => Task.FromResult(source.Source),
            exportAsync: (_, _, _, _) => Task.CompletedTask);

        await viewModel.AddImagesAsync(["large"]);

        var previewPackedImage = (PackedImage?)GetPrivateField(viewModel, "_previewPackedImage");

        Assert.NotNull(previewPackedImage);
        Assert.Equal(1024, previewPackedImage!.Width);
        Assert.Equal(512, previewPackedImage.Height);
        Assert.Equal("Output: 2048 x 1024", viewModel.OutputSizeText);
    }

    [Fact]
    public async Task PreviewModeChange_ReusesPackedPreviewCache()
    {
        var source = CreateSourceImage(
            Guid.Parse("90909090-9090-9090-9090-909090909090"),
            "packed.png",
            4,
            4,
            SourceChannelSet.Rgba,
            new Rgba32(10, 20, 30, 40));
        source.Detach();
        using var viewModel = new MainWindowViewModel(
            importAsync: (_, _) => Task.FromResult(source.Source),
            exportAsync: (_, _, _, _) => Task.CompletedTask);

        await viewModel.AddImagesAsync(["packed"]);
        var previewPackedImage = GetPrivateField(viewModel, "_previewPackedImage");

        viewModel.PreviewMode = PreviewMode.A;

        Assert.Same(previewPackedImage, GetPrivateField(viewModel, "_previewPackedImage"));
    }

    [Fact]
    public async Task ExportCommand_PacksOriginalOutputSize_WhenPreviewCacheIsDownscaled()
    {
        var source = CreateSourceImage(
            Guid.Parse("91919191-9191-9191-9191-919191919191"),
            "large.png",
            2048,
            1024,
            SourceChannelSet.Rgba,
            new Rgba32(10, 20, 30, 255));
        source.Detach();
        using var viewModel = new MainWindowViewModel(
            importAsync: (_, _) => Task.FromResult(source.Source),
            exportAsync: (packed, _, _, _) =>
            {
                Assert.Equal(2048, packed.Width);
                Assert.Equal(1024, packed.Height);
                return Task.CompletedTask;
            });

        await viewModel.AddImagesAsync(["large"]);
        await viewModel.ExportCommand.ExecuteAsync("C:/tmp/output.dds");
    }

    [Fact]
    public async Task SuggestedExportFileName_UsesTrimmedCommonSourcePrefix()
    {
        var color = CreateSourceImage(
            Guid.Parse("88888888-8888-8888-8888-888888888888"),
            "Grass005_1K-PNG_Color.png",
            2,
            2,
            SourceChannelSet.Rgb,
            new Rgba32(10, 20, 30, 255));
        var displacement = CreateSourceImage(
            Guid.Parse("99999999-9999-9999-9999-999999999999"),
            "Grass005_1K-PNG_Displacement.png",
            2,
            2,
            SourceChannelSet.Gray,
            new Rgba32(40, 40, 40, 255));
        color.Detach();
        displacement.Detach();

        using var viewModel = new MainWindowViewModel(
            importAsync: (path, _) => Task.FromResult(path == "color" ? color.Source : displacement.Source),
            exportAsync: (_, _, _, _) => Task.CompletedTask);

        await viewModel.AddImagesAsync(["color", "displacement"]);

        Assert.Equal("Grass005_1K-PNG.dds", viewModel.SuggestedExportFileName);
    }

    [Fact]
    public async Task SuggestedExportFileName_UsesSelectedExportExtension()
    {
        var source = CreateSourceImage(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            "Mask_RGB.png",
            2,
            2,
            SourceChannelSet.Rgba,
            new Rgba32(10, 20, 30, 255));
        source.Detach();
        using var viewModel = new MainWindowViewModel(
            importAsync: (_, _) => Task.FromResult(source.Source),
            exportAsync: (_, _, _, _) => Task.CompletedTask);

        await viewModel.AddImagesAsync(["mask"]);
        viewModel.SelectedExportFormat = ExportFormat.Png;

        Assert.Equal("Mask_RGB.png", viewModel.SuggestedExportFileName);
    }

    [Fact]
    public async Task SuggestedExportFileName_FallsBackToFirstSource_WhenCommonPrefixIsTooShort()
    {
        var color = CreateSourceImage(
            Guid.Parse("12121212-1212-1212-1212-121212121212"),
            "A_Color.png",
            2,
            2,
            SourceChannelSet.Rgb,
            new Rgba32(10, 20, 30, 255));
        var alpha = CreateSourceImage(
            Guid.Parse("34343434-3434-3434-3434-343434343434"),
            "B_Alpha.png",
            2,
            2,
            SourceChannelSet.Gray,
            new Rgba32(40, 40, 40, 255));
        color.Detach();
        alpha.Detach();

        using var viewModel = new MainWindowViewModel(
            importAsync: (path, _) => Task.FromResult(path == "color" ? color.Source : alpha.Source),
            exportAsync: (_, _, _, _) => Task.CompletedTask);

        await viewModel.AddImagesAsync(["color", "alpha"]);

        Assert.Equal("A_Color.dds", viewModel.SuggestedExportFileName);
    }

    [Fact]
    public async Task SuggestedExportDirectory_UsesFirstSuccessfullyImportedSourceDirectory()
    {
        var first = CreateSourceImage(
            Guid.Parse("45454545-4545-4545-4545-454545454545"),
            "first.png",
            2,
            2,
            SourceChannelSet.Rgb,
            new Rgba32(10, 20, 30, 255));
        var second = CreateSourceImage(
            Guid.Parse("56565656-5656-5656-5656-565656565656"),
            "second.png",
            2,
            2,
            SourceChannelSet.Gray,
            new Rgba32(40, 40, 40, 255));
        first.Detach();
        second.Detach();

        using var viewModel = new MainWindowViewModel(
            importAsync: (path, _) => Task.FromResult(path.Contains("first", StringComparison.OrdinalIgnoreCase) ? first.Source : second.Source),
            exportAsync: (_, _, _, _) => Task.CompletedTask);

        await viewModel.AddImagesAsync(
        [
            "D:/Textures/Grass/first.png",
            "E:/Other/second.png"
        ]);

        Assert.Equal(Path.GetFullPath("D:/Textures/Grass"), viewModel.SuggestedExportDirectory);
    }

    [Fact]
    public async Task ExportCommand_ReportsInlineStatus_WhenExporterFails()
    {
        var goodSource = CreateSourceImage(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "export.png",
            2,
            2,
            SourceChannelSet.Rgba,
            new Rgba32(40, 50, 60, 255));
        using var goodSourceScope = goodSource;
        var viewModel = new MainWindowViewModel(
            importAsync: (_, _) => Task.FromResult(goodSource.Source),
            exportAsync: (_, _, _, _) => Task.FromException(new InvalidOperationException("Disk full.")));

        await viewModel.AddImagesAsync(["C:/tmp/export.png"]);
        await viewModel.ExportCommand.ExecuteAsync("C:/tmp/output.png");

        Assert.Contains("Export failed", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Disk full", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dispose_ReleasesImportedImages_AndClearsPreviewResources()
    {
        var sourceScope = CreateSourceImage(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "dispose.png",
            2,
            2,
            SourceChannelSet.Rgba,
            new Rgba32(70, 80, 90, 255));
        var source = sourceScope.Source;
        sourceScope.Detach();

        var viewModel = new MainWindowViewModel(
            importAsync: (_, _) => Task.FromResult(source),
            exportAsync: (_, _, _, _) => Task.CompletedTask);

        await viewModel.AddImagesAsync(["C:/tmp/dispose.png"]);
        using var packedPixels = new Image<Rgba32>(2, 2);
        SetPrivateField(viewModel, "_previewPackedImage", new PackedImage(packedPixels.Clone(), hadResizedSources: false));

        viewModel.Dispose();

        var packedImage = (PackedImage?)GetPrivateField(viewModel, "_previewPackedImage");

        Assert.Null(viewModel.PreviewBitmap);
        Assert.Null(packedImage);
        Assert.Throws<ObjectDisposedException>(() => source.Pixels.Clone());
    }

    private static TestSourceScope CreateSourceImage(
        Guid id,
        string fileName,
        int width,
        int height,
        SourceChannelSet channelSet,
        Rgba32 color)
    {
        var pixels = new Image<Rgba32>(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                pixels[x, y] = color;
            }
        }

        return new TestSourceScope(new SourceImage(id, fileName, width, height, channelSet, pixels));
    }

    private sealed class TestSourceScope : IDisposable
    {
        private bool _ownsPixels = true;

        public TestSourceScope(SourceImage source)
        {
            Source = source;
        }

        public SourceImage Source { get; }

        public void Detach() => _ownsPixels = false;

        public void Dispose()
        {
            if (_ownsPixels)
            {
                Source.Pixels.Dispose();
            }
        }
    }

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private static object? GetPrivateField(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return field!.GetValue(instance);
    }

    private static object? InvokePrivateMethod(object instance, string methodName, params object?[] arguments)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method!.Invoke(instance, arguments);
    }
}
