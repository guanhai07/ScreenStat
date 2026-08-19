using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ScreenStat.DatasetTests;

internal static class DatasetImageLoader
{
    /// <summary>
    /// Reads a captured PNG back into the BGRA buffer layout the OCR services
    /// expect, matching how the app hands screen pixels to them.
    /// </summary>
    public static (byte[] Pixels, int Width, int Height) Load(string path)
    {
        using var bitmap = new Bitmap(path);
        var data = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            var pixels = new byte[Math.Abs(data.Stride) * bitmap.Height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            return (pixels, bitmap.Width, bitmap.Height);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}
