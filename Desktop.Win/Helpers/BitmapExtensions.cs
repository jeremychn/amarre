using SkiaSharp;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace Remotely.Desktop.Win.Helpers;

[SupportedOSPlatform("windows")]
public static class BitmapExtensions
{
    // Replaces SkiaSharp.Views.Desktop's ToSKBitmap: since SkiaSharp 3, it only ships for
    // net10.0-windows10.0.19041, and moving to that target would raise the minimum Windows version.
    public static SKBitmap ToSKBitmap(this Bitmap bitmap)
    {
        var info = new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var skBitmap = new SKBitmap(info);
        var data = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppPArgb);
        try
        {
            var source = data.Scan0;
            var destination = skBitmap.GetPixels();
            var rowBytes = Math.Min(data.Stride, skBitmap.RowBytes);
            for (var y = 0; y < bitmap.Height; y++)
            {
                unsafe
                {
                    Buffer.MemoryCopy((void*)source, (void*)destination, rowBytes, rowBytes);
                }
                source += data.Stride;
                destination += skBitmap.RowBytes;
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
        return skBitmap;
    }
}
