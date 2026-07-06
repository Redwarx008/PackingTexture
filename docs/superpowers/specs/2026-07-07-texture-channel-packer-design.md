# Avalonia Texture Channel Packer Design

## Goal

Build a Windows-friendly Avalonia desktop tool for packing channels from several source images into one output texture. The tool is aimed at game/material workflows, especially Stride projects that need mask textures, normal map convention fixes, PNG output, and DDS BC compressed output.

The first version should be a focused utility, not a full texture suite.

## Scope

In scope:

- Load several ordinary image files as sources.
- Default channel mapping by source order.
- Allow manual override of each output channel.
- Preview the packed result.
- Preview individual output channels.
- Export PNG.
- Export DDS using BC compression.
- Optionally generate mipmaps for DDS output.
- Optionally flip the green/Y channel for normal map DirectX/OpenGL convention conversion.

Out of scope for the first version:

- JPG export.
- DDS preview or DDS import.
- Per-channel invert controls.
- Separate auto-fill command button.
- ASTC, ETC, KTX, KTX2, or other mobile/Web texture container support.
- Batch processing.
- Project preset saving.

## Technology

Use a lightweight Avalonia MVVM application.

- Avalonia for UI.
- ImageSharp for reading source images, converting pixels, resizing, preview generation, and PNG export.
- BCnEncoder.NET for DDS BC compression and DDS file writing.

No external command line tools are required in the first version.

## Layout

The main window uses a two-zone utility layout.

Left zone: Sources and channel packing.

- Add Images button.
- Drag and drop image import area.
- Source image list with thumbnail, filename, dimensions, and available channel summary.
- Source image reorder support.
- Compact output channel table for R, G, B, and A.
- Each output row exposes source image and source channel selectors.

Right zone: Preview and export.

- Large packed-texture preview.
- Preview mode buttons: RGBA, R, G, B, A.
- Output size display.
- Export format selector.
- DDS mipmap checkbox.
- Flip Y / green channel checkbox.
- Size mismatch warning area.
- Export button labeled exactly `Export`.

There is no separate middle mapping panel. Channel mapping stays compact on the left so preview/export has more room.

## Channel Mapping

On import, the app automatically assigns source channels to output channels in order.

Example:

- First source image has RGB channels.
- Its R, G, and B channels fill output R, G, and B.
- The next source image supplies the next available source channel for output A.

Manual overrides remain available through the channel table. Each output channel can choose:

- Source image.
- Source channel.

Supported source channel choices:

- R
- G
- B
- A, when available
- Gray, for single-channel or luminance use
- Zero
- One

No per-channel invert option is included.

Automatic mapping is the default state, not a command button. Adding sources or reordering sources recomputes channels that are still in automatic mode. If the user manually changes an output channel, that channel becomes manually mapped and should not be overwritten by later source reordering unless its referenced source is removed.

If there are not enough source channels to fill all four output channels, the remaining RGB channels default to Zero and the remaining alpha channel defaults to One.

## Image Size Policy

The first imported source image defines the output size.

When later sources have different dimensions, the app scales them to the output size for packing and shows a warning in the export/preview area. This keeps the common workflow fast while still making dimension mismatches visible.

## Preview Behavior

The preview updates after:

- Adding source images.
- Reordering source images.
- Changing an output channel source image.
- Changing an output channel source channel.
- Toggling Flip Y.

Preview modes:

- RGBA: composite output preview over a checkerboard background.
- R/G/B/A: grayscale single-channel output preview.

Preview does not read or decode DDS files. It always shows the in-memory packed output before export.

## Export Behavior

Supported first-version outputs:

- PNG.
- DDS BC1.
- DDS BC3.
- DDS BC4.
- DDS BC5.
- DDS BC7.

DDS export uses BCnEncoder.NET. The UI exposes mipmap generation as an export option for DDS formats.

`Flip Y / green channel` modifies the packed output's green channel before preview/export. This is intended for normal map convention conversion between DirectX and OpenGL style Y directions.

## Error Handling

The app should show clear inline messages for:

- Unsupported image format.
- Failed file load.
- Empty source list.
- Unable to resolve a channel mapping, such as when a manually referenced source was removed.
- Source channel unavailable.
- Image resize warning.
- Export failure.

Recoverable issues should not crash the app. The user should be able to remove or replace a bad input and continue.

## Architecture

The UI should stay thin. Image work belongs in services.

Suggested structure:

- `Models/SourceImage`
- `Models/ChannelMapping`
- `Models/ExportSettings`
- `Services/ImageImportService`
- `Services/ChannelPackingService`
- `Services/TextureExportService`
- `ViewModels/MainWindowViewModel`
- `Views/MainWindow`

`ChannelPackingService` should be testable without Avalonia. It takes source image pixel data and channel mappings, then returns packed RGBA pixel data.

`TextureExportService` should handle PNG and DDS export from already packed pixel data.

## Testing

Focus tests on core image behavior rather than Avalonia visual details.

Recommended tests:

- Default mapping fills output R/G/B/A from source channel order.
- Manual mapping chooses the requested source channel.
- Zero and One channel constants work.
- Size mismatch sources resize to the first image's dimensions.
- Flip Y changes the green output channel as expected.
- Export rejects empty or incomplete input state with a clear error.

Manual verification:

- App opens.
- Multiple images can be added.
- Drag and drop works.
- Reordering sources updates default mapping.
- Preview updates for mapping changes.
- PNG export writes a valid image.
- DDS BC7 export writes a valid DDS file.

## Acceptance Criteria

- A user can add two images where the first supplies RGB and the second supplies alpha/gray, then export one packed texture.
- The UI defaults to source-order channel packing without needing an Auto Fill button.
- The user can manually override any output channel source.
- The preview can switch between RGBA and individual channel views.
- PNG and DDS BC compressed export work.
- The export button text is exactly `Export`.
- No JPG export appears in the UI.
- No DDS import or DDS preview is implemented.
