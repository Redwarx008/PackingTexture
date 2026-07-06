# Texture Channel Packer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an Avalonia desktop tool that loads ordinary images, packs selected source channels into RGBA, previews the result, and exports PNG or DDS BC compressed textures.

**Architecture:** Use a thin Avalonia MVVM app backed by a testable core class library. Core services own source image import, automatic/manual channel mapping, preview pixel generation, and PNG/DDS export. The UI binds to a single main view model and does no pixel manipulation directly.

**Tech Stack:** .NET 8 target, Avalonia, CommunityToolkit.Mvvm, SixLabors.ImageSharp, BCnEncoder.Net, BCnEncoder.Net.ImageSharp, xUnit.

## Global Constraints

- Target `net8.0` even though newer SDKs are installed, because Avalonia supports .NET 8+ and this keeps the app broadly usable.
- Do not implement JPG export.
- Do not implement DDS import or DDS preview.
- Do not add per-channel invert controls.
- Do not add an Auto Fill button; automatic source-order mapping is default behavior.
- Support exports: PNG, DDS BC1, DDS BC3, DDS BC4, DDS BC5, DDS BC7.
- The export button text must be exactly `Export`.
- The first imported source image defines output size.
- Later source images with different dimensions resize to the first image dimensions and surface a warning.
- `Flip Y / green channel` means `G = 255 - G` on packed output before preview/export.
- Keep `.superpowers/` ignored.

## Reference Notes

- Avalonia templates are not installed locally yet. Install them with `dotnet new install Avalonia.Templates`.
- Local SDK check showed .NET SDK `10.0.301`, with .NET 8 runtime/SDK also installed.
- BCnEncoder.NET supports BC1 through BC7 and DDS output, and its ImageSharp extension exposes `BcEncoder.EncodeToStream(image, stream)` with `OutputOptions.Format`, `OutputOptions.FileFormat`, `OutputOptions.GenerateMipMaps`, and `OutputOptions.Quality`.

## Planned File Structure

- `PackingTexture.sln` - solution file.
- `src/PackingTexture.App/PackingTexture.App.csproj` - Avalonia desktop app.
- `src/PackingTexture.App/App.axaml` - app resources and theme.
- `src/PackingTexture.App/App.axaml.cs` - Avalonia app startup.
- `src/PackingTexture.App/Program.cs` - desktop entry point.
- `src/PackingTexture.App/Views/MainWindow.axaml` - two-zone UI layout.
- `src/PackingTexture.App/Views/MainWindow.axaml.cs` - view constructor only.
- `src/PackingTexture.App/ViewModels/MainWindowViewModel.cs` - commands, observable state, UI orchestration.
- `src/PackingTexture.Core/PackingTexture.Core.csproj` - testable core library.
- `src/PackingTexture.Core/Models/ChannelId.cs` - output/source channel enum.
- `src/PackingTexture.Core/Models/ChannelMapping.cs` - output channel mapping model.
- `src/PackingTexture.Core/Models/ChannelSourceKind.cs` - source channel vs constants.
- `src/PackingTexture.Core/Models/ExportFormat.cs` - PNG and DDS BC export enum.
- `src/PackingTexture.Core/Models/ExportSettings.cs` - export settings model.
- `src/PackingTexture.Core/Models/PackedImage.cs` - packed RGBA result.
- `src/PackingTexture.Core/Models/SourceImage.cs` - imported source image model.
- `src/PackingTexture.Core/Services/ChannelPackingService.cs` - automatic mapping and RGBA packing.
- `src/PackingTexture.Core/Services/ImageImportService.cs` - ImageSharp import/resize/thumbnail support.
- `src/PackingTexture.Core/Services/TextureExportService.cs` - PNG and DDS output.
- `tests/PackingTexture.Core.Tests/PackingTexture.Core.Tests.csproj` - xUnit tests.
- `tests/PackingTexture.Core.Tests/ChannelPackingServiceTests.cs` - mapping/packing tests.
- `tests/PackingTexture.Core.Tests/TextureExportServiceTests.cs` - export validation tests.

---

### Task 1: Solution And Project Scaffold

**Files:**
- Create: `PackingTexture.sln`
- Create: `src/PackingTexture.Core/PackingTexture.Core.csproj`
- Create: `src/PackingTexture.App/PackingTexture.App.csproj`
- Create: `tests/PackingTexture.Core.Tests/PackingTexture.Core.Tests.csproj`
- Modify: `.gitignore`

**Interfaces:**
- Consumes: existing design spec at `docs/superpowers/specs/2026-07-07-texture-channel-packer-design.md`
- Produces: buildable solution with app, core library, and core test project.

- [ ] **Step 1: Install Avalonia templates**

Run:

```powershell
dotnet new install Avalonia.Templates
```

Expected: output includes `Avalonia.Templates` and new templates become available.

- [ ] **Step 2: Create solution and projects**

Run:

```powershell
dotnet new sln -n PackingTexture
dotnet new classlib -n PackingTexture.Core -o src/PackingTexture.Core -f net8.0
dotnet new avalonia.app -n PackingTexture.App -o src/PackingTexture.App -f net8.0
dotnet new xunit -n PackingTexture.Core.Tests -o tests/PackingTexture.Core.Tests -f net8.0
dotnet sln PackingTexture.sln add src/PackingTexture.Core/PackingTexture.Core.csproj
dotnet sln PackingTexture.sln add src/PackingTexture.App/PackingTexture.App.csproj
dotnet sln PackingTexture.sln add tests/PackingTexture.Core.Tests/PackingTexture.Core.Tests.csproj
dotnet add src/PackingTexture.App/PackingTexture.App.csproj reference src/PackingTexture.Core/PackingTexture.Core.csproj
dotnet add tests/PackingTexture.Core.Tests/PackingTexture.Core.Tests.csproj reference src/PackingTexture.Core/PackingTexture.Core.csproj
```

Expected: each command succeeds and `PackingTexture.sln` lists all three projects.

- [ ] **Step 3: Add package references**

Run:

```powershell
dotnet add src/PackingTexture.App/PackingTexture.App.csproj package CommunityToolkit.Mvvm
dotnet add src/PackingTexture.Core/PackingTexture.Core.csproj package SixLabors.ImageSharp
dotnet add src/PackingTexture.Core/PackingTexture.Core.csproj package BCnEncoder.Net
dotnet add src/PackingTexture.Core/PackingTexture.Core.csproj package BCnEncoder.Net.ImageSharp
dotnet add tests/PackingTexture.Core.Tests/PackingTexture.Core.Tests.csproj package SixLabors.ImageSharp
```

Expected: restore succeeds.

- [ ] **Step 4: Remove template placeholder files**

Delete:

```text
src/PackingTexture.Core/Class1.cs
tests/PackingTexture.Core.Tests/UnitTest1.cs
```

Expected: no placeholder class/test files remain.

- [ ] **Step 5: Build and test empty scaffold**

Run:

```powershell
dotnet build PackingTexture.sln
dotnet test PackingTexture.sln
```

Expected: build succeeds; tests pass or report no tests beyond xUnit infrastructure.

- [ ] **Step 6: Commit**

Run:

```powershell
git add PackingTexture.sln src tests .gitignore
git commit -m "chore: scaffold Avalonia texture packer solution"
```

Expected: commit succeeds.

---

### Task 2: Core Models And Automatic Mapping

**Files:**
- Create: `src/PackingTexture.Core/Models/ChannelId.cs`
- Create: `src/PackingTexture.Core/Models/ChannelSourceKind.cs`
- Create: `src/PackingTexture.Core/Models/ChannelMapping.cs`
- Create: `src/PackingTexture.Core/Models/SourceImage.cs`
- Create: `src/PackingTexture.Core/Services/ChannelPackingService.cs`
- Test: `tests/PackingTexture.Core.Tests/ChannelPackingServiceTests.cs`

**Interfaces:**
- Consumes: no prior code beyond project scaffold.
- Produces:
  - `ChannelPackingService.CreateDefaultMappings(IReadOnlyList<SourceImage>) : IReadOnlyList<ChannelMapping>`
  - `ChannelMapping.ForSource(ChannelId output, Guid sourceId, ChannelId sourceChannel, bool isAutomatic = true) : ChannelMapping`
  - `ChannelMapping.ForConstant(ChannelId output, ChannelSourceKind constantKind, bool isAutomatic = true) : ChannelMapping`

- [ ] **Step 1: Write failing tests for automatic mapping and constants**

Create `tests/PackingTexture.Core.Tests/ChannelPackingServiceTests.cs`:

```csharp
using PackingTexture.Core.Models;
using PackingTexture.Core.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PackingTexture.Core.Tests;

public sealed class ChannelPackingServiceTests
{
    [Fact]
    public void CreateDefaultMappings_FillsRgbFromFirstImageAndAlphaFromSecond()
    {
        var firstId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var secondId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        using var rgb = new Image<Rgba32>(2, 2);
        using var gray = new Image<Rgba32>(2, 2);

        var sources = new[]
        {
            new SourceImage(firstId, "Mask_RGB.png", 2, 2, SourceChannelSet.Rgb, rgb),
            new SourceImage(secondId, "Alpha.png", 2, 2, SourceChannelSet.Gray, gray)
        };

        var mappings = ChannelPackingService.CreateDefaultMappings(sources);

        Assert.Equal(4, mappings.Count);
        Assert.Equal(ChannelId.R, mappings[0].OutputChannel);
        Assert.Equal(firstId, mappings[0].SourceImageId);
        Assert.Equal(ChannelId.R, mappings[0].SourceChannel);
        Assert.Equal(ChannelId.G, mappings[1].SourceChannel);
        Assert.Equal(ChannelId.B, mappings[2].SourceChannel);
        Assert.Equal(secondId, mappings[3].SourceImageId);
        Assert.Equal(ChannelId.Gray, mappings[3].SourceChannel);
    }

    [Fact]
    public void CreateDefaultMappings_DefaultsMissingRgbToZeroAndAlphaToOne()
    {
        var sourceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        using var redOnly = new Image<Rgba32>(2, 2);
        var sources = new[]
        {
            new SourceImage(sourceId, "OnlyR.png", 2, 2, SourceChannelSet.Red, redOnly)
        };

        var mappings = ChannelPackingService.CreateDefaultMappings(sources);

        Assert.Equal(ChannelId.R, mappings[0].SourceChannel);
        Assert.Equal(ChannelSourceKind.Zero, mappings[1].SourceKind);
        Assert.Equal(ChannelSourceKind.Zero, mappings[2].SourceKind);
        Assert.Equal(ChannelSourceKind.One, mappings[3].SourceKind);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test tests/PackingTexture.Core.Tests/PackingTexture.Core.Tests.csproj --filter ChannelPackingServiceTests
```

Expected: fails because model and service types do not exist.

- [ ] **Step 3: Add core model types**

Create `src/PackingTexture.Core/Models/ChannelId.cs`:

```csharp
namespace PackingTexture.Core.Models;

public enum ChannelId
{
    R,
    G,
    B,
    A,
    Gray
}
```

Create `src/PackingTexture.Core/Models/ChannelSourceKind.cs`:

```csharp
namespace PackingTexture.Core.Models;

public enum ChannelSourceKind
{
    SourceChannel,
    Zero,
    One
}
```

Create `src/PackingTexture.Core/Models/SourceChannelSet.cs`:

```csharp
namespace PackingTexture.Core.Models;

public enum SourceChannelSet
{
    Red,
    Gray,
    Rgb,
    Rgba
}
```

Create `src/PackingTexture.Core/Models/SourceImage.cs`:

```csharp
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PackingTexture.Core.Models;

public sealed record SourceImage(
    Guid Id,
    string FileName,
    int Width,
    int Height,
    SourceChannelSet Channels,
    Image<Rgba32> Pixels)
{
    public IReadOnlyList<ChannelId> AvailableChannels => Channels switch
    {
        SourceChannelSet.Red => [ChannelId.R],
        SourceChannelSet.Gray => [ChannelId.Gray],
        SourceChannelSet.Rgb => [ChannelId.R, ChannelId.G, ChannelId.B],
        SourceChannelSet.Rgba => [ChannelId.R, ChannelId.G, ChannelId.B, ChannelId.A],
        _ => [ChannelId.R, ChannelId.G, ChannelId.B, ChannelId.A]
    };
}
```

Create `src/PackingTexture.Core/Models/ChannelMapping.cs`:

```csharp
namespace PackingTexture.Core.Models;

public sealed record ChannelMapping(
    ChannelId OutputChannel,
    ChannelSourceKind SourceKind,
    Guid? SourceImageId,
    ChannelId? SourceChannel,
    bool IsAutomatic)
{
    public static ChannelMapping ForSource(
        ChannelId output,
        Guid sourceId,
        ChannelId sourceChannel,
        bool isAutomatic = true) =>
        new(output, ChannelSourceKind.SourceChannel, sourceId, sourceChannel, isAutomatic);

    public static ChannelMapping ForConstant(
        ChannelId output,
        ChannelSourceKind constantKind,
        bool isAutomatic = true)
    {
        if (constantKind is ChannelSourceKind.SourceChannel)
        {
            throw new ArgumentException("Use ForSource for source-channel mappings.", nameof(constantKind));
        }

        return new(output, constantKind, null, null, isAutomatic);
    }
}
```

- [ ] **Step 4: Add default mapping service**

Create `src/PackingTexture.Core/Services/ChannelPackingService.cs`:

```csharp
using PackingTexture.Core.Models;

namespace PackingTexture.Core.Services;

public static class ChannelPackingService
{
    private static readonly ChannelId[] OutputOrder = [ChannelId.R, ChannelId.G, ChannelId.B, ChannelId.A];

    public static IReadOnlyList<ChannelMapping> CreateDefaultMappings(IReadOnlyList<SourceImage> sources)
    {
        var sourceChannels = sources
            .SelectMany(source => source.AvailableChannels.Select(channel => (source.Id, Channel: channel)))
            .ToList();

        var result = new List<ChannelMapping>(4);
        for (var index = 0; index < OutputOrder.Length; index++)
        {
            var output = OutputOrder[index];
            if (index < sourceChannels.Count)
            {
                var source = sourceChannels[index];
                result.Add(ChannelMapping.ForSource(output, source.Id, source.Channel));
                continue;
            }

            result.Add(ChannelMapping.ForConstant(
                output,
                output is ChannelId.A ? ChannelSourceKind.One : ChannelSourceKind.Zero));
        }

        return result;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run:

```powershell
dotnet test tests/PackingTexture.Core.Tests/PackingTexture.Core.Tests.csproj --filter ChannelPackingServiceTests
```

Expected: both tests pass.

- [ ] **Step 6: Commit**

Run:

```powershell
git add src/PackingTexture.Core tests/PackingTexture.Core.Tests
git commit -m "feat: add channel mapping core models"
```

Expected: commit succeeds.

---

### Task 3: Channel Packing Pixel Output

**Files:**
- Create: `src/PackingTexture.Core/Models/PackedImage.cs`
- Modify: `src/PackingTexture.Core/Services/ChannelPackingService.cs`
- Test: `tests/PackingTexture.Core.Tests/ChannelPackingServiceTests.cs`

**Interfaces:**
- Consumes:
  - `SourceImage`
  - `ChannelMapping`
- Produces:
  - `ChannelPackingService.Pack(IReadOnlyList<SourceImage> sources, IReadOnlyList<ChannelMapping> mappings, bool flipGreen) : PackedImage`

- [ ] **Step 1: Add failing tests for packing, manual mapping, resize, and flip Y**

Append to `ChannelPackingServiceTests`:

```csharp
[Fact]
public void Pack_UsesManualSourceChannelMapping()
{
    var sourceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    using var image = new Image<Rgba32>(1, 1);
    image[0, 0] = new Rgba32(10, 20, 30, 40);
    var source = new SourceImage(sourceId, "Source.png", 1, 1, SourceChannelSet.Rgba, image);

    var mappings = new[]
    {
        ChannelMapping.ForSource(ChannelId.R, sourceId, ChannelId.B, isAutomatic: false),
        ChannelMapping.ForSource(ChannelId.G, sourceId, ChannelId.G, isAutomatic: false),
        ChannelMapping.ForConstant(ChannelId.B, ChannelSourceKind.Zero, isAutomatic: false),
        ChannelMapping.ForConstant(ChannelId.A, ChannelSourceKind.One, isAutomatic: false)
    };

    using var packed = ChannelPackingService.Pack([source], mappings, flipGreen: false);

    Assert.Equal(new Rgba32(30, 20, 0, 255), packed.Pixels[0, 0]);
}

[Fact]
public void Pack_FlipGreenInvertsGreenChannel()
{
    var sourceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    using var image = new Image<Rgba32>(1, 1);
    image[0, 0] = new Rgba32(0, 32, 0, 255);
    var source = new SourceImage(sourceId, "Normal.png", 1, 1, SourceChannelSet.Rgba, image);
    var mappings = new[]
    {
        ChannelMapping.ForConstant(ChannelId.R, ChannelSourceKind.Zero),
        ChannelMapping.ForSource(ChannelId.G, sourceId, ChannelId.G),
        ChannelMapping.ForConstant(ChannelId.B, ChannelSourceKind.Zero),
        ChannelMapping.ForConstant(ChannelId.A, ChannelSourceKind.One)
    };

    using var packed = ChannelPackingService.Pack([source], mappings, flipGreen: true);

    Assert.Equal((byte)223, packed.Pixels[0, 0].G);
}

[Fact]
public void Pack_ResizesNonPrimarySourcesToFirstSourceSize()
{
    var firstId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    var secondId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    using var first = new Image<Rgba32>(2, 2);
    using var second = new Image<Rgba32>(1, 1);
    first[0, 0] = new Rgba32(1, 0, 0, 255);
    second[0, 0] = new Rgba32(0, 99, 0, 255);

    var sources = new[]
    {
        new SourceImage(firstId, "First.png", 2, 2, SourceChannelSet.Rgba, first),
        new SourceImage(secondId, "Second.png", 1, 1, SourceChannelSet.Rgba, second)
    };
    var mappings = new[]
    {
        ChannelMapping.ForSource(ChannelId.R, firstId, ChannelId.R),
        ChannelMapping.ForSource(ChannelId.G, secondId, ChannelId.G),
        ChannelMapping.ForConstant(ChannelId.B, ChannelSourceKind.Zero),
        ChannelMapping.ForConstant(ChannelId.A, ChannelSourceKind.One)
    };

    using var packed = ChannelPackingService.Pack(sources, mappings, flipGreen: false);

    Assert.Equal(2, packed.Width);
    Assert.Equal(2, packed.Height);
    Assert.Equal((byte)99, packed.Pixels[1, 1].G);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test tests/PackingTexture.Core.Tests/PackingTexture.Core.Tests.csproj --filter ChannelPackingServiceTests
```

Expected: fails because `PackedImage` and `Pack` do not exist.

- [ ] **Step 3: Add packed image model**

Create `src/PackingTexture.Core/Models/PackedImage.cs`:

```csharp
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PackingTexture.Core.Models;

public sealed class PackedImage : IDisposable
{
    public PackedImage(Image<Rgba32> pixels, bool hadResizedSources)
    {
        Pixels = pixels;
        HadResizedSources = hadResizedSources;
    }

    public Image<Rgba32> Pixels { get; }
    public int Width => Pixels.Width;
    public int Height => Pixels.Height;
    public bool HadResizedSources { get; }

    public void Dispose() => Pixels.Dispose();
}
```

- [ ] **Step 4: Implement packing**

Replace `ChannelPackingService.cs` with:

```csharp
using PackingTexture.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace PackingTexture.Core.Services;

public static class ChannelPackingService
{
    private static readonly ChannelId[] OutputOrder = [ChannelId.R, ChannelId.G, ChannelId.B, ChannelId.A];

    public static IReadOnlyList<ChannelMapping> CreateDefaultMappings(IReadOnlyList<SourceImage> sources)
    {
        var sourceChannels = sources
            .SelectMany(source => source.AvailableChannels.Select(channel => (source.Id, Channel: channel)))
            .ToList();

        var result = new List<ChannelMapping>(4);
        for (var index = 0; index < OutputOrder.Length; index++)
        {
            var output = OutputOrder[index];
            if (index < sourceChannels.Count)
            {
                var source = sourceChannels[index];
                result.Add(ChannelMapping.ForSource(output, source.Id, source.Channel));
                continue;
            }

            result.Add(ChannelMapping.ForConstant(
                output,
                output is ChannelId.A ? ChannelSourceKind.One : ChannelSourceKind.Zero));
        }

        return result;
    }

    public static PackedImage Pack(
        IReadOnlyList<SourceImage> sources,
        IReadOnlyList<ChannelMapping> mappings,
        bool flipGreen)
    {
        if (sources.Count == 0)
        {
            throw new InvalidOperationException("Add at least one source image before packing.");
        }

        if (mappings.Count != 4)
        {
            throw new InvalidOperationException("Exactly four output channel mappings are required.");
        }

        var width = sources[0].Width;
        var height = sources[0].Height;
        var hadResizedSources = sources.Any(source => source.Width != width || source.Height != height);
        using var resizedSources = new DisposableImageLookup(sources, width, height);
        var output = new Image<Rgba32>(width, height);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var r = ResolveByte(mappings.Single(m => m.OutputChannel == ChannelId.R), resizedSources, x, y);
                var g = ResolveByte(mappings.Single(m => m.OutputChannel == ChannelId.G), resizedSources, x, y);
                var b = ResolveByte(mappings.Single(m => m.OutputChannel == ChannelId.B), resizedSources, x, y);
                var a = ResolveByte(mappings.Single(m => m.OutputChannel == ChannelId.A), resizedSources, x, y);

                if (flipGreen)
                {
                    g = (byte)(255 - g);
                }

                output[x, y] = new Rgba32(r, g, b, a);
            }
        }

        return new PackedImage(output, hadResizedSources);
    }

    private static byte ResolveByte(ChannelMapping mapping, DisposableImageLookup images, int x, int y)
    {
        if (mapping.SourceKind == ChannelSourceKind.Zero)
        {
            return 0;
        }

        if (mapping.SourceKind == ChannelSourceKind.One)
        {
            return 255;
        }

        if (mapping.SourceImageId is null || mapping.SourceChannel is null)
        {
            throw new InvalidOperationException($"Output channel {mapping.OutputChannel} has no source.");
        }

        var pixel = images[mapping.SourceImageId.Value][x, y];
        return mapping.SourceChannel.Value switch
        {
            ChannelId.R => pixel.R,
            ChannelId.G => pixel.G,
            ChannelId.B => pixel.B,
            ChannelId.A => pixel.A,
            ChannelId.Gray => (byte)Math.Round((pixel.R + pixel.G + pixel.B) / 3.0),
            _ => throw new InvalidOperationException($"Unsupported source channel {mapping.SourceChannel}.")
        };
    }

    private sealed class DisposableImageLookup : IDisposable
    {
        private readonly Dictionary<Guid, Image<Rgba32>> _images = new();
        private readonly List<Image<Rgba32>> _owned = new();

        public DisposableImageLookup(IReadOnlyList<SourceImage> sources, int width, int height)
        {
            foreach (var source in sources)
            {
                if (source.Width == width && source.Height == height)
                {
                    _images[source.Id] = source.Pixels;
                    continue;
                }

                var clone = source.Pixels.Clone(ctx => ctx.Resize(width, height));
                _owned.Add(clone);
                _images[source.Id] = clone;
            }
        }

        public Image<Rgba32> this[Guid id] =>
            _images.TryGetValue(id, out var image)
                ? image
                : throw new InvalidOperationException("A mapped source image is no longer available.");

        public void Dispose()
        {
            foreach (var image in _owned)
            {
                image.Dispose();
            }
        }
    }
}
```

- [ ] **Step 5: Run tests**

Run:

```powershell
dotnet test tests/PackingTexture.Core.Tests/PackingTexture.Core.Tests.csproj --filter ChannelPackingServiceTests
```

Expected: all channel packing tests pass.

- [ ] **Step 6: Commit**

Run:

```powershell
git add src/PackingTexture.Core tests/PackingTexture.Core.Tests
git commit -m "feat: pack source channels into RGBA output"
```

Expected: commit succeeds.

---

### Task 4: Import And Export Services

**Files:**
- Create: `src/PackingTexture.Core/Models/ExportFormat.cs`
- Create: `src/PackingTexture.Core/Models/ExportSettings.cs`
- Create: `src/PackingTexture.Core/Services/ImageImportService.cs`
- Create: `src/PackingTexture.Core/Services/TextureExportService.cs`
- Test: `tests/PackingTexture.Core.Tests/TextureExportServiceTests.cs`

**Interfaces:**
- Consumes:
  - `PackedImage`
  - `SourceImage`
- Produces:
  - `ImageImportService.ImportAsync(string path, CancellationToken cancellationToken) : Task<SourceImage>`
  - `TextureExportService.ExportAsync(PackedImage image, string path, ExportSettings settings, CancellationToken cancellationToken) : Task`

- [ ] **Step 1: Add failing export tests**

Create `tests/PackingTexture.Core.Tests/TextureExportServiceTests.cs`:

```csharp
using PackingTexture.Core.Models;
using PackingTexture.Core.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PackingTexture.Core.Tests;

public sealed class TextureExportServiceTests
{
    [Fact]
    public async Task ExportAsync_WritesPng()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        using var pixels = new Image<Rgba32>(2, 2);
        pixels[0, 0] = new Rgba32(1, 2, 3, 255);
        using var packed = new PackedImage(pixels, hadResizedSources: false);

        await TextureExportService.ExportAsync(
            packed,
            path,
            new ExportSettings(ExportFormat.Png, GenerateMipMaps: false),
            CancellationToken.None);

        Assert.True(File.Exists(path));
        using var reloaded = await Image.LoadAsync<Rgba32>(path);
        Assert.Equal(2, reloaded.Width);
        Assert.Equal(2, reloaded.Height);
        File.Delete(path);
    }

    [Fact]
    public async Task ExportAsync_RejectsDdsPathWithPngFormat()
    {
        using var pixels = new Image<Rgba32>(2, 2);
        using var packed = new PackedImage(pixels, hadResizedSources: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TextureExportService.ExportAsync(
                packed,
                "bad.dds",
                new ExportSettings(ExportFormat.Png, GenerateMipMaps: false),
                CancellationToken.None));

        Assert.Contains("extension", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test tests/PackingTexture.Core.Tests/PackingTexture.Core.Tests.csproj --filter TextureExportServiceTests
```

Expected: fails because export models/service do not exist.

- [ ] **Step 3: Add export models**

Create `src/PackingTexture.Core/Models/ExportFormat.cs`:

```csharp
namespace PackingTexture.Core.Models;

public enum ExportFormat
{
    Png,
    DdsBc1,
    DdsBc3,
    DdsBc4,
    DdsBc5,
    DdsBc7
}
```

Create `src/PackingTexture.Core/Models/ExportSettings.cs`:

```csharp
namespace PackingTexture.Core.Models;

public sealed record ExportSettings(ExportFormat Format, bool GenerateMipMaps);
```

- [ ] **Step 4: Add image import service**

Create `src/PackingTexture.Core/Services/ImageImportService.cs`:

```csharp
using PackingTexture.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PackingTexture.Core.Services;

public static class ImageImportService
{
    public static async Task<SourceImage> ImportAsync(string path, CancellationToken cancellationToken)
    {
        var image = await Image.LoadAsync<Rgba32>(path, cancellationToken);
        var fileName = Path.GetFileName(path);
        var channels = DetectChannels(image);
        return new SourceImage(Guid.NewGuid(), fileName, image.Width, image.Height, channels, image);
    }

    private static SourceChannelSet DetectChannels(Image<Rgba32> image)
    {
        var hasAlpha = false;
        var isGray = true;

        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var pixel = image[x, y];
                hasAlpha |= pixel.A < 255;
                isGray &= pixel.R == pixel.G && pixel.G == pixel.B;
            }
        }

        if (hasAlpha)
        {
            return SourceChannelSet.Rgba;
        }

        return isGray ? SourceChannelSet.Gray : SourceChannelSet.Rgb;
    }
}
```

- [ ] **Step 5: Add texture export service**

Create `src/PackingTexture.Core/Services/TextureExportService.cs`:

```csharp
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

        await using var stream = File.Create(path);
        if (settings.Format == ExportFormat.Png)
        {
            await image.Pixels.SaveAsPngAsync(stream, cancellationToken);
            return;
        }

        var encoder = new BcEncoder
        {
            OutputOptions =
            {
                FileFormat = OutputFileFormat.Dds,
                GenerateMipMaps = settings.GenerateMipMaps,
                Quality = CompressionQuality.Balanced,
                Format = ToCompressionFormat(settings.Format)
            }
        };

        encoder.EncodeToStream(image.Pixels, stream);
    }

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
```

- [ ] **Step 6: Run export tests**

Run:

```powershell
dotnet test tests/PackingTexture.Core.Tests/PackingTexture.Core.Tests.csproj --filter TextureExportServiceTests
```

Expected: tests pass. If `CompressionFormat.Bc7` name differs in the installed BCnEncoder version, inspect the installed enum and adjust the mapping only.

- [ ] **Step 7: Run full core tests**

Run:

```powershell
dotnet test tests/PackingTexture.Core.Tests/PackingTexture.Core.Tests.csproj
```

Expected: all tests pass.

- [ ] **Step 8: Commit**

Run:

```powershell
git add src/PackingTexture.Core tests/PackingTexture.Core.Tests
git commit -m "feat: import images and export PNG DDS textures"
```

Expected: commit succeeds.

---

### Task 5: Main ViewModel

**Files:**
- Create: `src/PackingTexture.App/ViewModels/MainWindowViewModel.cs`
- Modify: `src/PackingTexture.App/PackingTexture.App.csproj`

**Interfaces:**
- Consumes:
  - `ImageImportService.ImportAsync`
  - `ChannelPackingService.CreateDefaultMappings`
  - `ChannelPackingService.Pack`
  - `TextureExportService.ExportAsync`
- Produces:
  - `MainWindowViewModel.SourceImages`
  - `MainWindowViewModel.Mappings`
  - `MainWindowViewModel.ExportFormats`
  - `MainWindowViewModel.PreviewMode`
  - `MainWindowViewModel.ExportCommand`
  - `MainWindowViewModel.AddImagesAsync`

- [ ] **Step 1: Ensure MVVM generator package exists**

Run:

```powershell
dotnet list src/PackingTexture.App/PackingTexture.App.csproj package
```

Expected: `CommunityToolkit.Mvvm` appears. If it does not, run:

```powershell
dotnet add src/PackingTexture.App/PackingTexture.App.csproj package CommunityToolkit.Mvvm
```

- [ ] **Step 2: Add view model**

Create `src/PackingTexture.App/ViewModels/MainWindowViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PackingTexture.Core.Models;
using PackingTexture.Core.Services;

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
}

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly List<SourceImage> _sources = [];
    private PackedImage? _packedImage;

    [ObservableProperty]
    private Bitmap? previewBitmap;

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
        PreviewBitmap = new Bitmap(stream);
    }
}
```

- [ ] **Step 3: Add preview image helper**

Create `src/PackingTexture.App/ViewModels/PreviewImageFactory.cs`:

```csharp
using PackingTexture.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PackingTexture.App.ViewModels;

public static class PreviewImageFactory
{
    public static Image<Rgba32> Create(PackedImage packed, PreviewMode mode)
    {
        var clone = packed.Pixels.Clone();
        if (mode == PreviewMode.Rgba)
        {
            return clone;
        }

        for (var y = 0; y < clone.Height; y++)
        {
            for (var x = 0; x < clone.Width; x++)
            {
                var pixel = clone[x, y];
                var value = mode switch
                {
                    PreviewMode.R => pixel.R,
                    PreviewMode.G => pixel.G,
                    PreviewMode.B => pixel.B,
                    PreviewMode.A => pixel.A,
                    _ => pixel.R
                };
                clone[x, y] = new Rgba32(value, value, value, 255);
            }
        }

        return clone;
    }
}
```

- [ ] **Step 4: Build**

Run:

```powershell
dotnet build PackingTexture.sln
```

Expected: build succeeds. If Avalonia `Bitmap` conflicts with ImageSharp types, fully qualify `Avalonia.Media.Imaging.Bitmap`.

- [ ] **Step 5: Commit**

Run:

```powershell
git add src/PackingTexture.App
git commit -m "feat: add main texture packer view model"
```

Expected: commit succeeds.

---

### Task 6: Avalonia Main Window UI

**Files:**
- Modify: `src/PackingTexture.App/App.axaml`
- Modify: `src/PackingTexture.App/Views/MainWindow.axaml`
- Modify: `src/PackingTexture.App/Views/MainWindow.axaml.cs`

**Interfaces:**
- Consumes: `MainWindowViewModel`
- Produces: usable two-zone desktop UI with source list, mapping table, preview modes, export options, `Export` button.

- [ ] **Step 1: Set app theme**

Modify `src/PackingTexture.App/App.axaml` to include Fluent theme:

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="PackingTexture.App.App"
             RequestedThemeVariant="Default">
  <Application.Styles>
    <FluentTheme />
  </Application.Styles>
</Application>
```

- [ ] **Step 2: Add main window XAML**

Replace `src/PackingTexture.App/Views/MainWindow.axaml` with:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:PackingTexture.App.ViewModels"
        x:Class="PackingTexture.App.Views.MainWindow"
        x:DataType="vm:MainWindowViewModel"
        Title="PackingTexture"
        Width="1180"
        Height="720"
        MinWidth="980"
        MinHeight="620">
  <Design.DataContext>
    <vm:MainWindowViewModel />
  </Design.DataContext>

  <Grid ColumnDefinitions="460,*" RowDefinitions="*" Margin="12" ColumnSpacing="12">
    <Border Grid.Column="0" BorderBrush="#CBD5E1" BorderThickness="1" CornerRadius="6" Background="#F8FAFC">
      <DockPanel>
        <Grid DockPanel.Dock="Top" ColumnDefinitions="*,Auto" Margin="12">
          <TextBlock Text="Sources &amp; Channel Packing" FontWeight="SemiBold" VerticalAlignment="Center" />
          <Button Grid.Column="1" Content="Add Images" Click="AddImages_OnClick" />
        </Grid>

        <Border DockPanel.Dock="Top" Margin="12,0,12,12" Padding="12" BorderBrush="#BFDBFE" BorderThickness="1" Background="#EFF6FF" CornerRadius="6">
          <TextBlock Text="Drag PNG / TGA / BMP / JPG source images here" HorizontalAlignment="Center" Foreground="#475569" />
        </Border>

        <ListBox DockPanel.Dock="Top" ItemsSource="{Binding SourceImages}" Height="170" Margin="12,0,12,12">
          <ListBox.ItemTemplate>
            <DataTemplate>
              <Grid ColumnDefinitions="*,Auto" Margin="4">
                <StackPanel>
                  <TextBlock Text="{Binding FileName}" FontWeight="SemiBold" />
                  <TextBlock Text="{Binding Dimensions}" Foreground="#64748B" FontSize="12" />
                  <TextBlock Text="{Binding ChannelSummary}" Foreground="#64748B" FontSize="12" />
                </StackPanel>
                <TextBlock Grid.Column="1" Text="&#x2261;" Foreground="#64748B" VerticalAlignment="Center" />
              </Grid>
            </DataTemplate>
          </ListBox.ItemTemplate>
        </ListBox>

        <StackPanel Margin="12" Spacing="8">
          <Grid ColumnDefinitions="*,Auto">
            <TextBlock Text="Output Channels" FontWeight="SemiBold" />
            <TextBlock Grid.Column="1" Text="Auto assigned from source order" Foreground="#64748B" FontSize="12" />
          </Grid>

          <ItemsControl ItemsSource="{Binding Mappings}">
            <ItemsControl.ItemTemplate>
              <DataTemplate>
                <Grid ColumnDefinitions="42,*,110" Margin="0,4" ColumnSpacing="8">
                  <TextBlock Text="{Binding OutputChannel}" FontWeight="Bold" FontSize="20" HorizontalAlignment="Center" />
                  <ComboBox SelectedItem="{Binding Mapping.SourceImageId}" />
                  <ComboBox Grid.Column="2" SelectedItem="{Binding Mapping.SourceChannel}" />
                </Grid>
              </DataTemplate>
            </ItemsControl.ItemTemplate>
          </ItemsControl>
        </StackPanel>
      </DockPanel>
    </Border>

    <Border Grid.Column="1" BorderBrush="#CBD5E1" BorderThickness="1" CornerRadius="6" Background="#F8FAFC">
      <DockPanel>
        <Grid DockPanel.Dock="Top" ColumnDefinitions="*,Auto" Margin="12">
          <TextBlock Text="Preview &amp; Export" FontWeight="SemiBold" />
          <TextBlock Grid.Column="1" Text="{Binding StatusText}" Foreground="#64748B" FontSize="12" />
        </Grid>

        <Border Margin="12" BorderBrush="#CBD5E1" BorderThickness="1" CornerRadius="6" Background="#E2E8F0" Height="380">
          <Image Source="{Binding PreviewBitmap}" Stretch="Uniform" />
        </Border>

        <StackPanel DockPanel.Dock="Bottom" Margin="12" Spacing="10">
          <StackPanel Orientation="Horizontal" Spacing="6">
            <RadioButton Content="RGBA" GroupName="Preview" IsChecked="True" />
            <RadioButton Content="R" GroupName="Preview" />
            <RadioButton Content="G" GroupName="Preview" />
            <RadioButton Content="B" GroupName="Preview" />
            <RadioButton Content="A" GroupName="Preview" />
          </StackPanel>

          <Grid ColumnDefinitions="*,*" ColumnSpacing="12">
            <StackPanel Spacing="6">
              <TextBlock Text="Format" FontWeight="SemiBold" />
              <ComboBox ItemsSource="{Binding ExportFormats}" SelectedItem="{Binding SelectedExportFormat}" />
            </StackPanel>

            <StackPanel Grid.Column="1" Spacing="6">
              <TextBlock Text="Options" FontWeight="SemiBold" />
              <CheckBox Content="Mipmaps" IsChecked="{Binding GenerateMipMaps}" />
              <CheckBox Content="Flip Y / green channel" IsChecked="{Binding FlipY}" />
            </StackPanel>
          </Grid>

          <Button Content="Export" HorizontalAlignment="Stretch" HorizontalContentAlignment="Center" Click="Export_OnClick" />
        </StackPanel>
      </DockPanel>
    </Border>
  </Grid>
</Window>
```

- [ ] **Step 3: Wire file dialogs in code-behind**

Modify `src/PackingTexture.App/Views/MainWindow.axaml.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Interactivity;
using PackingTexture.App.ViewModels;

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

        var dialog = new OpenFileDialog
        {
            AllowMultiple = true,
            Filters =
            {
                new FileDialogFilter
                {
                    Name = "Images",
                    Extensions = { "png", "jpg", "jpeg", "bmp", "tga" }
                }
            }
        };

        var files = await dialog.ShowAsync(this);
        if (files is { Length: > 0 })
        {
            await viewModel.AddImagesAsync(files);
        }
    }

    private async void Export_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var extension = viewModel.SelectedExportFormat == Core.Models.ExportFormat.Png ? "png" : "dds";
        var dialog = new SaveFileDialog
        {
            DefaultExtension = extension,
            InitialFileName = $"packed.{extension}"
        };

        var file = await dialog.ShowAsync(this);
        if (!string.IsNullOrWhiteSpace(file) && viewModel.ExportCommand.CanExecute(file))
        {
            await viewModel.ExportCommand.ExecuteAsync(file);
        }
    }
}
```

- [ ] **Step 4: Build**

Run:

```powershell
dotnet build PackingTexture.sln
```

Expected: build succeeds. If current Avalonia version has moved from `OpenFileDialog`/`SaveFileDialog` to storage provider APIs, replace only the dialog code while keeping `AddImagesAsync` and `ExportCommand` unchanged.

- [ ] **Step 5: Commit**

Run:

```powershell
git add src/PackingTexture.App
git commit -m "feat: add texture packer main window"
```

Expected: commit succeeds.

---

### Task 7: Drag Drop, Reorder, And Final Verification

**Files:**
- Modify: `src/PackingTexture.App/Views/MainWindow.axaml`
- Modify: `src/PackingTexture.App/Views/MainWindow.axaml.cs`
- Modify: `src/PackingTexture.App/ViewModels/MainWindowViewModel.cs`

**Interfaces:**
- Consumes: existing app UI and `MainWindowViewModel.AddImagesAsync`
- Produces: drag/drop import, basic source reorder commands, final verified app.

- [ ] **Step 1: Add reorder commands**

Append to `MainWindowViewModel`:

```csharp
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
    RefreshPreview();
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
    RefreshPreview();
}
```

- [ ] **Step 2: Add drag/drop handlers**

Modify the top-level `Window` in `MainWindow.axaml`:

```xml
<Window ...
        DragDrop.AllowDrop="True"
        DragDrop.Drop="Window_OnDrop">
```

Add to `MainWindow.axaml.cs`:

```csharp
using Avalonia.Input;

private async void Window_OnDrop(object? sender, DragEventArgs e)
{
    if (DataContext is not MainWindowViewModel viewModel)
    {
        return;
    }

    var files = e.Data.GetFiles()?
        .Select(file => file.Path.LocalPath)
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .ToArray();

    if (files is { Length: > 0 })
    {
        await viewModel.AddImagesAsync(files);
    }
}
```

- [ ] **Step 3: Add reorder buttons to source list**

In the source list item template, replace the right-side text glyph with:

```xml
<StackPanel Grid.Column="1" Orientation="Vertical" Spacing="4">
  <Button Content="Up" Command="{Binding $parent[Window].DataContext.MoveSourceUpCommand}" CommandParameter="{Binding}" />
  <Button Content="Down" Command="{Binding $parent[Window].DataContext.MoveSourceDownCommand}" CommandParameter="{Binding}" />
</StackPanel>
```

Expected: source list can be reordered without an Auto Fill button.

- [ ] **Step 4: Build and test**

Run:

```powershell
dotnet build PackingTexture.sln
dotnet test PackingTexture.sln
```

Expected: build succeeds and all tests pass.

- [ ] **Step 5: Run the app**

Run:

```powershell
dotnet run --project src/PackingTexture.App/PackingTexture.App.csproj
```

Expected:

- Main window opens.
- Left header has `Add Images`.
- No `Auto Fill` button is visible.
- No JPG export option is visible.
- Export button text is exactly `Export`.
- Format choices include PNG, DDS BC1, DDS BC3, DDS BC4, DDS BC5, DDS BC7.
- Options include `Mipmaps` and `Flip Y / green channel`.

- [ ] **Step 6: Manual export smoke test**

Use the running app:

```text
1. Add a small RGB PNG.
2. Add a small grayscale PNG.
3. Confirm mappings default to R/G/B from the first source and A/Gray from the second.
4. Switch preview to R, G, B, and A.
5. Toggle Flip Y and confirm preview changes.
6. Export PNG.
7. Export DDS BC7.
```

Expected: PNG and DDS files are written. No DDS preview/import is attempted.

- [ ] **Step 7: Commit**

Run:

```powershell
git add src/PackingTexture.App
git commit -m "feat: support drag drop reorder and final app flow"
```

Expected: commit succeeds.

---

## Final Verification

Run:

```powershell
dotnet build PackingTexture.sln
dotnet test PackingTexture.sln
dotnet run --project src/PackingTexture.App/PackingTexture.App.csproj
```

Expected:

- Build succeeds.
- Tests pass.
- App launches.
- UI matches the approved two-zone design.
- Export flow works for PNG and DDS BC7.

## Self-Review

- Spec coverage: all approved scope items are covered by Tasks 2-7.
- Out of scope checks: JPG, DDS import/preview, per-channel invert, Auto Fill button, ASTC/ETC/KTX/KTX2, batch processing, and presets are excluded.
- Type consistency: `SourceImage`, `ChannelMapping`, `PackedImage`, `ExportSettings`, `ExportFormat`, `ChannelPackingService`, `ImageImportService`, and `TextureExportService` names are consistent across tasks.
- Known implementation risk: Avalonia file dialog APIs may differ depending on the installed template version. The plan isolates dialog code in `MainWindow.axaml.cs`, so any adjustment does not affect core services.
