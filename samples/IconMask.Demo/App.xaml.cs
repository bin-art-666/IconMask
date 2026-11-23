using System;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI.ViewManagement;
using Drawing = System.Drawing;

namespace IconMask.Demo;

public partial class App : Application
{
    private Window? window;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var accessibility = new AccessibilitySettings();
        window = new Window { Title = "图标遮罩" };
        window.AppWindow.Resize(new Windows.Graphics.SizeInt32(640, 460));

        var choice = new ComboBox { Header = "底板", HorizontalAlignment = HorizontalAlignment.Left };
        choice.Items.Add("浅色");
        choice.Items.Add("深色");
        choice.SelectedIndex = 0;

        var original = new CheckBox { Content = "使用原始图标" };
        var contrast = new CheckBox { Content = "模拟高对比度" };
        var transparentSample = CreateColumn("透明图标", out var transparentImage);
        var platedSample = CreateColumn("自带底板", out var platedImage);
        var examples = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 40 };
        examples.Children.Add(transparentSample);
        examples.Children.Add(platedSample);

        var content = new StackPanel { Padding = new Thickness(24), Spacing = 16 };
        content.Children.Add(choice);
        content.Children.Add(examples);
        content.Children.Add(original);
        content.Children.Add(contrast);
        window.Content = new ScrollViewer { Content = content };

        void Refresh()
        {
            var tone = choice.SelectedIndex == 1 ? IconTone.Dark : IconTone.Light;
            bool highContrast = accessibility.HighContrast || contrast.IsChecked == true;
            using var transparent = SampleIcon(false);
            using var plated = SampleIcon(true);
            transparentImage.Source = RenderImage(transparent, tone, original.IsChecked == true, highContrast);
            platedImage.Source = RenderImage(plated, tone, original.IsChecked == true, highContrast);
        }

        choice.SelectionChanged += (_, _) => Refresh();
        original.Click += (_, _) => Refresh();
        contrast.Click += (_, _) => Refresh();
        window.Activated += (_, _) => Refresh();
        Refresh();
        window.Activate();
    }

    private static StackPanel CreateColumn(string label, out Image image)
    {
        image = new Image { Width = 104, Height = 104 };
        var column = new StackPanel { Spacing = 8 };
        column.Children.Add(image);
        column.Children.Add(new TextBlock { Text = label, HorizontalAlignment = HorizontalAlignment.Center });
        return column;
    }

    private static Drawing.Bitmap SampleIcon(bool hasPlate)
    {
        var bitmap = new Drawing.Bitmap(hasPlate ? 104 : 112, 104);
        using var graphics = Drawing.Graphics.FromImage(bitmap);
        graphics.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
        if (hasPlate)
            graphics.Clear(Drawing.Color.FromArgb(41, 85, 159));

        using var brush = new Drawing.SolidBrush(hasPlate ? Drawing.Color.White : Drawing.Color.FromArgb(19, 48, 68));
        int center = bitmap.Width / 2;
        graphics.FillPolygon(brush, [
            new(center, 21), new(center + 10, 43), new(center + 32, 52), new(center + 10, 61),
            new(center, 83), new(center - 10, 61), new(center - 32, 52), new(center - 10, 43),
        ]);
        return bitmap;
    }

    private static BitmapImage RenderImage(Drawing.Bitmap source, IconTone tone, bool original, bool highContrast)
    {
        using var bitmap = IconMaskRenderer.Render(source, 208, tone, original, highContrast);
        using var png = new MemoryStream();
        bitmap.Save(png, ImageFormat.Png);
        png.Position = 0;
        using var stream = png.AsRandomAccessStream();
        var image = new BitmapImage();
        image.SetSource(stream);
        return image;
    }
}
