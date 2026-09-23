using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VoiceTyper.Services;

public static class TrayIconFactory
{
    public static ImageSource Create()
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = new Uri("pack://application:,,,/Assets/icon.png", UriKind.Absolute);
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.DecodePixelWidth = 256;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }
}
