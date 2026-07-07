# PackingTexture

PackingTexture 是一个基于 Avalonia 和 .NET 的贴图通道打包工具，用来把多张图片的通道组合到一张贴图里。它既可以作为桌面 GUI 使用，也可以作为命令行批处理工具使用。

典型用途：把一张 Mask 贴图里的 RGB 数据和另一张高度/Alpha 贴图里的灰度数据，打包成一张 PNG 或 DDS 贴图。

## 功能

- Avalonia 桌面 GUI。
- 可重复批处理的 CLI。
- 输入图片：PNG、JPG/JPEG、TGA、BMP，以及其它 ImageSharp 支持的格式。
- 输出格式：PNG、DDS BC1、DDS BC3、DDS BC4、DDS BC5、DDS BC7。
- 默认通道分配按输入图片顺序填充：R -> G -> B -> A。
- 可以手动指定每个输出通道的来源图片和来源通道。
- CLI 映射语法支持单通道反相。
- 支持 Flip Y / green channel，用于 DirectX/OpenGL 法线图方向适配。
- 支持 RGBA、R、G、B、A 预览模式。
- 预览使用降采样缓存，切换通道更快；导出仍使用原始分辨率。
- 第一张输入图片决定输出尺寸，其它图片会缩放到第一张图片尺寸。

## GUI

运行 Avalonia 应用：

```powershell
dotnet run --project src\PackingTexture.App
```

GUI 使用流程：

1. 点击 `Add Images`，或把源图片拖到源图片列表里。
2. 按需要调整源图片顺序。默认映射会根据源图片顺序重新生成，同时保留手动映射。
3. 如果默认映射不符合需求，手动调整输出通道行。
4. 选择预览模式：`RGBA`、`R`、`G`、`B` 或 `A`。
5. 选择输出格式和选项。
6. 点击 `Export`。

导出文件名会尽量从输入文件的公共前缀推断。默认导出目录是第一张成功导入图片所在的目录。

## CLI

运行命令行打包工具：

```powershell
dotnet run --project src\PackingTexture.Cli -- pack `
  -i Grass005_1K-PNG_Color.png `
  -i Grass005_1K-PNG_Displacement.png `
  --format dds-bc7 `
  -o Grass005_1K-PNG.dds `
  --mipmaps
```

如果不指定手动通道选项，CLI 会使用和 GUI 一样的默认映射：按输入顺序依次取可用源通道，填入输出 R、G、B、A。

### 手动映射

使用 `--r`、`--g`、`--b`、`--a` 覆盖输出通道：

```powershell
dotnet run --project src\PackingTexture.Cli -- pack `
  -i Grass005_1K-PNG_Color.png `
  -i Grass005_1K-PNG_Displacement.png `
  --r 0:r `
  --g 0:g `
  --b 0:b `
  --a 1:gray `
  --format dds-bc7 `
  -o Grass005_1K-PNG.dds
```

映射语法：

```text
<源图片索引>:<通道>
<源图片索引>:<通道>!
0
1
```

示例：

```text
0:r       第 1 张输入图片的 R 通道
0:g!      第 1 张输入图片的 G 通道，并反相
1:gray    第 2 张输入图片的灰度值
1:a!      第 2 张输入图片的 A 通道，并反相
0         常量黑
1         常量白
```

支持的源通道：

```text
r, g, b, a, gray
```

### CLI 选项

```text
pack
  -i, --input <path>     添加输入图片。可以重复传入。
  -o, --output <path>    输出路径。可选。
  --format <format>      png, dds-bc1, dds-bc3, dds-bc4, dds-bc5, dds-bc7。
  --r <mapping>          覆盖输出 R。
  --g <mapping>          覆盖输出 G。
  --b <mapping>          覆盖输出 B。
  --a <mapping>          覆盖输出 A。
  --mipmaps              为 DDS 输出生成 mipmaps。
  --no-mipmaps           禁用 mipmaps。
  --flip-y               反相输出 G 通道。
  -h, --help             显示帮助。
```

如果省略 `--output`，CLI 会输出到第一张输入图片所在目录，并根据输入文件的公共前缀推断文件名。PNG 输出使用 `.png`，DDS 输出使用 `.dds`。

## 格式通道限制

部分压缩格式不会保存全部四个输出通道。PackingTexture 会根据所选格式屏蔽或拒绝无效通道。

| 格式 | 有效输出通道 |
| --- | --- |
| PNG | R, G, B, A |
| DDS BC1 | R, G, B |
| DDS BC3 | R, G, B, A |
| DDS BC4 | R |
| DDS BC5 | R, G |
| DDS BC7 | R, G, B, A |

在 GUI 中，无效映射行会被禁用；预览和导出会把无效 RGB 通道置黑，把无效 Alpha 通道置白。

在 CLI 中，如果手动映射了无效通道，会直接报错。例如 `--format dds-bc5 --b 0:b` 会被拒绝，因为 BC5 只支持 R 和 G。

## 构建和测试

构建解决方案：

```powershell
dotnet build PackingTexture.sln
```

运行测试：

```powershell
dotnet test PackingTexture.sln --no-build
```

## 项目结构

```text
src/PackingTexture.Core   共享的图片导入、通道打包和导出逻辑。
src/PackingTexture.App    Avalonia 桌面 GUI。
src/PackingTexture.Cli    命令行前端。
tests                     Core 行为、GUI ViewModel 和 CLI 执行测试。
```

## 说明

- DDS 导出使用 BCnEncoder.Net.ImageSharp。
- 图片加载和 PNG 导出使用 SixLabors ImageSharp。
- GUI 预览为了交互性能可能会降采样；导出始终使用原始输出尺寸。
