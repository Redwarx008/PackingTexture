# Task 5 Report: Main ViewModel

## What I changed

- Added `src/PackingTexture.App/ViewModels/MainWindowViewModel.cs` with:
  - `PreviewMode`
  - `SourceImageViewModel`
  - `ChannelMappingViewModel`
  - `MainWindowViewModel`
- Added `src/PackingTexture.App/ViewModels/PreviewImageFactory.cs` to build preview images from `PackedImage` in the selected preview mode.
- The app project already referenced `CommunityToolkit.Mvvm`, so no `.csproj` package edit was needed.

## Behavior

- `MainWindowViewModel.AddImagesAsync` imports images with `ImageImportService.ImportAsync`, stores them in `SourceImages`, regenerates default mappings, and refreshes the preview.
- `MainWindowViewModel.Mappings` is populated through `ChannelPackingService.CreateDefaultMappings`.
- `MainWindowViewModel.RefreshPreview` uses `ChannelPackingService.Pack` and surfaces `PackedImage.HadResizedSources` in `StatusText`:
  - `Some sources were resized to match the first image.`
  - `Ready.`
- `MainWindowViewModel.ExportCommand` is generated from `[RelayCommand]` and exports through `TextureExportService.ExportAsync`.
- `PreviewImageFactory` clones packed pixels and renders grayscale channel previews for `R`, `G`, `B`, and `A`.

## Verification

- Confirmed `CommunityToolkit.Mvvm` was already present with:
  - `dotnet list src/PackingTexture.App/PackingTexture.App.csproj package`
- Ran the solution build:
  - `dotnet build PackingTexture.sln`
  - Result: succeeded, 0 warnings, 0 errors

## Commit

- `89f6734` - `feat: add main texture packer view model`

## Concerns

- No outstanding concerns.

## Fix

- Wired `ChannelMappingViewModel` to call back into `MainWindowViewModel.RefreshPreview()` whenever `Mapping` is replaced, and passed that callback when creating mappings so later manual edits now repack `_packedImage` and refresh `PreviewBitmap`.
- Replaced the generated `PreviewBitmap` property with a manual setter that disposes the previous `Avalonia.Media.Imaging.Bitmap` only after the new value has been assigned, so replacing or clearing the preview no longer leaks the old bitmap.
- Kept `ApplyAutomaticMappings()` responsible for the refresh after rebuilding mappings, and removed the duplicate refresh from `AddImagesAsync()`.

## Verification

- `dotnet build PackingTexture.sln`
- Result: succeeded, 0 warnings, 0 errors
