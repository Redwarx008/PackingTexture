using System.Collections.ObjectModel;
using System.Reflection;
using PackingTexture.App.ViewModels;
using PackingTexture.Core.Models;
using PackingTexture.Core.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PackingTexture.Core.Tests;

public sealed class ChannelMappingViewModelTests
{
    [Fact]
    public void SourceOptions_IncludeConstants_AndFilterChannelsBySelectedImage()
    {
        var redOnly = CreateSourceImageViewModel(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "RedOnly.png",
            SourceChannelSet.Red);
        var gray = CreateSourceImageViewModel(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            "Gray.png",
            SourceChannelSet.Gray);
        var mapping = ChannelMapping.ForSource(ChannelId.R, redOnly.Id, ChannelId.R, isAutomatic: false);
        var viewModel = new ChannelMappingViewModel(mapping, () => { });
        InvokeMethod(viewModel, "AttachSourceImages", new object?[] { new[] { redOnly, gray } });

        var sourceOptions = GetProperty<IReadOnlyList<object>>(viewModel, "SourceOptions");

        Assert.Contains(sourceOptions, option => GetProperty<string>(option, "DisplayName") == "Zero");
        Assert.Contains(sourceOptions, option => GetProperty<string>(option, "DisplayName") == "One");
        Assert.Contains(sourceOptions, option => GetProperty<string>(option, "DisplayName") == "RedOnly.png");

        var channels = GetProperty<IReadOnlyList<ChannelId>>(viewModel, "AvailableSourceChannels");
        Assert.Equal([ChannelId.R], channels);

        SetProperty(viewModel, "SelectedSourceOption", sourceOptions.Single(option => GetProperty<string>(option, "DisplayName") == "Gray.png"));

        channels = GetProperty<IReadOnlyList<ChannelId>>(viewModel, "AvailableSourceChannels");
        Assert.Equal([ChannelId.Gray], channels);
    }

    [Fact]
    public void SelectedSourceOption_RebuildsMapping_AndRefreshesPreview()
    {
        var redOnly = CreateSourceImageViewModel(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "RedOnly.png",
            SourceChannelSet.Red);
        var rgba = CreateSourceImageViewModel(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            "Rgba.png",
            SourceChannelSet.Rgba);
        var mapping = ChannelMapping.ForSource(ChannelId.G, redOnly.Id, ChannelId.R, isAutomatic: false);
        var refreshCount = 0;
        var viewModel = new ChannelMappingViewModel(mapping, () => refreshCount++);
        InvokeMethod(viewModel, "AttachSourceImages", new object?[] { new[] { redOnly, rgba } });

        var sourceOptions = GetProperty<IReadOnlyList<object>>(viewModel, "SourceOptions");
        var rgbaOption = sourceOptions.Single(option => GetProperty<string>(option, "DisplayName") == "Rgba.png");

        SetProperty(viewModel, "SelectedSourceOption", rgbaOption);
        SetProperty(viewModel, "SelectedSourceChannel", ChannelId.B);

        var rebuiltMapping = GetProperty<ChannelMapping>(viewModel, "Mapping");
        Assert.Equal(rgba.Id, rebuiltMapping.SourceImageId);
        Assert.Equal(ChannelId.B, rebuiltMapping.SourceChannel);
        Assert.Equal(ChannelSourceKind.SourceChannel, rebuiltMapping.SourceKind);
        Assert.True(refreshCount > 0);
    }

    [Fact]
    public void RebuildMappingsForSourceOrder_PreservesManualMappings_AndRecomputesAutomaticMappings()
    {
        var firstId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var secondId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        using var first = new Image<Rgba32>(1, 1);
        using var second = new Image<Rgba32>(1, 1);

        var sources = new[]
        {
            new SourceImage(secondId, "Second.png", 1, 1, SourceChannelSet.Gray, second.Clone()),
            new SourceImage(firstId, "First.png", 1, 1, SourceChannelSet.Red, first.Clone())
        };
        var existingMappings = new[]
        {
            ChannelMapping.ForSource(ChannelId.R, firstId, ChannelId.R),
            ChannelMapping.ForConstant(ChannelId.G, ChannelSourceKind.Zero, isAutomatic: false),
            ChannelMapping.ForConstant(ChannelId.B, ChannelSourceKind.Zero),
            ChannelMapping.ForConstant(ChannelId.A, ChannelSourceKind.One)
        };

        var method = typeof(ChannelPackingService).GetMethod(
            "RebuildMappingsForSourceOrder",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var rebuilt = (IReadOnlyList<ChannelMapping>)method!.Invoke(null, [sources, existingMappings])!;

        var red = rebuilt.Single(mapping => mapping.OutputChannel == ChannelId.R);
        var green = rebuilt.Single(mapping => mapping.OutputChannel == ChannelId.G);

        Assert.Equal(secondId, red.SourceImageId);
        Assert.Equal(ChannelId.Gray, red.SourceChannel);
        Assert.True(red.IsAutomatic);
        Assert.Equal(ChannelSourceKind.Zero, green.SourceKind);
        Assert.Null(green.SourceImageId);
        Assert.Null(green.SourceChannel);
        Assert.False(green.IsAutomatic);
    }

    private static SourceImageViewModel CreateSourceImageViewModel(Guid id, string fileName, SourceChannelSet channels)
    {
        using var pixels = new Image<Rgba32>(1, 1);
        var source = new SourceImage(id, fileName, 1, 1, channels, pixels.Clone());
        return new SourceImageViewModel(source);
    }

    private static T GetProperty<T>(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(property);
        return (T)property!.GetValue(instance)!;
    }

    private static void SetProperty(object instance, string propertyName, object? value)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(property);
        property!.SetValue(instance, value);
    }

    private static void InvokeMethod(object instance, string methodName, params object?[] arguments)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(method);
        method!.Invoke(instance, arguments);
    }
}
