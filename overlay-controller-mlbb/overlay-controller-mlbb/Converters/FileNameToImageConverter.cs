using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using MlbbOverlay.Services;

namespace MlbbOverlay.Converters;

public class FileNameToImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string name || string.IsNullOrWhiteSpace(name)) return null;

        var parts = (parameter as string ?? "hero").Split('|');
        var kind = parts[0];
        var width = parts.Length > 1 && int.TryParse(parts[1], out var w) ? w : 0;

        var path = kind switch
        {
            "path" => name,
            "logo" => Path.Combine(AppPaths.LogoImages, name),
            "asset" => Path.Combine(AppPaths.OverlayAssets, name),
            _ => Path.Combine(AppPaths.HeroImages, name)
        };

        if (!File.Exists(path)) return null;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;                  
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;   
            if (width > 0) bitmap.DecodePixelWidth = width;
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;   
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}