using System.Windows.Media.Imaging;
using System.Windows.Media;
using SkiaSharp;
using System.Windows;

namespace TestTask
{
    static class DrawingHelper
    {
        public static WriteableBitmap CreateImage(int width, int height)
        {
            var writeableBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, BitmapPalettes.Halftone256Transparent);
            return writeableBitmap;
        }

        public static void DrawOnCanvas(WriteableBitmap writeableBitmap, DrawContent DrawContent)
        {
            int width = (int)writeableBitmap.Width,
            height = (int)writeableBitmap.Height;
            writeableBitmap.Lock();

            var skImageInfo = new SKImageInfo()
            {
                Width = width,
                Height = height,
                ColorType = SKColorType.Bgra8888,
                AlphaType = SKAlphaType.Premul,
                ColorSpace = SKColorSpace.CreateSrgb()
            };

            using var surface = SKSurface.Create(skImageInfo, writeableBitmap.BackBuffer);
            SKCanvas canvas = surface.Canvas;
            canvas.Clear();

            DrawContent(canvas);
            
            writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
            writeableBitmap.Unlock();
        }
    }
}
