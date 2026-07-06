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

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private static object? InvokePrivateMethod(object instance, string methodName, params object?[] arguments)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method!.Invoke(instance, arguments);
    }
}
