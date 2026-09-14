using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VoiceTyper.Services;

public static class TrayIconFactory
{
    public static ImageSource Create()
    {
        const int size = 32;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRoundedRectangle(
                new SolidColorBrush(Color.FromRgb(0x1F, 0x1A, 0x16)),
                new Pen(new SolidColorBrush(Color.FromRgb(0xC6, 0xA3, 0x6A)), 1.2),
                new Rect(1, 1, size - 2, size - 2),
                7, 7);

            var mic = new SolidColorBrush(Color.FromRgb(0xF4, 0xED, 0xE3));
            dc.DrawRoundedRectangle(mic, null, new Rect(13, 7, 6, 11), 3, 3);
            dc.DrawRectangle(mic, null, new Rect(15, 18, 2, 5));
            dc.DrawRectangle(mic, null, new Rect(12, 22, 8, 2));
        }

        var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }
}
