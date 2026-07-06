# Task 3 Report: Channel Packing Pixel Output

## Summary
Implemented `ChannelPackingService.Pack(...)` and the new `PackedImage` model so source channels can be packed into RGBA output pixels.

## What Changed
- Added `PackedImage` with `Pixels`, `Width`, `Height`, `HadResizedSources`, and `IDisposable` support.
- Extended `ChannelPackingService` with `Pack(...)`.
- Kept default mapping creation intact.
- Added tests for:
  - manual source-channel mapping
  - green-channel inversion
  - resizing non-primary sources to the first source size

## Behavior
- Requires at least one source image.
- Requires exactly four output mappings.
- Resizes non-primary sources to the first source's dimensions before packing.
- Supports constant zero/one channels and source channels, including grayscale source channels.
- Optionally flips the packed green channel.

## Verification
- `dotnet test tests/PackingTexture.Core.Tests/PackingTexture.Core.Tests.csproj`
- Result: 5 tests passed, 0 failed, 0 skipped

## Commit
- `7ef6628` - `feat: pack source channels into RGBA output`

## Notes
- No outstanding concerns.

## Fix
- Validated all four output mappings against the available source images before creating the packed output image.
- Kept a defensive disposal path so the packed output is cleaned up if an unexpected exception still occurs after allocation.
- Added a focused regression test that `Pack` throws for a mapping that references a missing source image.

## Verification
- `dotnet test tests/PackingTexture.Core.Tests/PackingTexture.Core.Tests.csproj --filter ChannelPackingServiceTests`
- Result: 6 tests passed, 0 failed, 0 skipped
