# IconMask

用于 Windows 桌面应用的图标遮罩组件。传入位图，生成带透明圆角的方形图标。透明图标可加浅色或深色底板，自带底板的图标保留底色并按比例填充。也可在原图模式或高对比度下跳过遮罩和配色。

## 接入

需要 Windows 和 .NET 10 SDK。把 `src/IconMask/IconMask.csproj` 添加为项目引用，在调用方负责从文件或其他来源取得位图。

```csharp
using System.Drawing;
using IconMask;

using var source = new Bitmap("my-icon.png");
using var result = IconMaskRenderer.Render(source, size: 128, IconTone.Dark);
result.Save("masked.png", System.Drawing.Imaging.ImageFormat.Png);
```

`size` 为输出像素边长。调用方释放输出位图和输入位图。使用 `useOriginal: true` 或 `highContrast: true` 时，原图按比例居中缩放到透明方形画布。高对比度状态由宿主提供。对 WinUI 3，可参考示例中的 `RenderImage`，用 PNG 内存流转换为 `BitmapImage`。

## 运行示例

示例需要 Windows 11 x64，自带两种测试图案。界面可切换明暗底板、原图或模拟高对比度，窗口激活时也会重新读取 Windows 高对比度设置。

```powershell
dotnet publish .\samples\IconMask.Demo\IconMask.Demo.csproj -c Release -o dist
.\dist\IconMask.Demo.exe
```

## 来源

像素边界、底板识别和颜色处理提取自 AITaskbar，基线提交为 `a50a58df82cfe06fb00bc4210b0d05126c794202`，主要参考其中的 `Preview/AppIconArtwork.cs`、`Preview/TaskbarAppArtwork.cs` 和 `Preview/TaskbarIconOutline.cs`。组件使用位图作为输入，AITaskbar 的应用查找和个别应用图标修正由原项目负责。
