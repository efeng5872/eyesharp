using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CreateAppIcon
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            string outputDir = args.Length > 0 ? args[0] : "../../Resources/Icons";

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            Console.WriteLine("生成应用程序图标...");
            Console.WriteLine($"输出目录: {Path.GetFullPath(outputDir)}");

            // 生成多尺寸位图
            int[] sizes = { 16, 32, 48, 64, 128, 256 };
            var bitmaps = new List<(int size, byte[] pngData)>();

            foreach (var size in sizes)
            {
                Console.WriteLine($"生成 {size}x{size} 尺寸...");
                var bitmap = GenerateEyeIcon(size);
                var pngData = SavePngToBytes(bitmap);
                bitmaps.Add((size, pngData));
            }

            // 生成ICO文件
            string icoPath = Path.Combine(outputDir, "app.ico");
            CreateIcoFile(bitmaps, icoPath);

            Console.WriteLine($"应用程序图标已生成: {icoPath}");
        }

        static BitmapSource GenerateEyeIcon(int size)
        {
            double scale = size / 64.0;

            var drawingVisual = new DrawingVisual();
            using (var dc = drawingVisual.RenderOpen())
            {
                // 透明背景
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, size, size));

                double centerX = size / 2;
                double centerY = size / 2;

                // 眼睛尺寸（根据总尺寸调整）
                double eyeWidth = size * 0.7;
                double eyeHeight = size * 0.4;

                // 眼睛渐变 - 护眼绿色主题
                var eyeBrush = new RadialGradientBrush(
                    Color.FromRgb(129, 199, 132),  // 中心亮绿
                    Color.FromRgb(76, 175, 80)      // 边缘绿
                );
                eyeBrush.Center = new Point(0.5, 0.5);
                eyeBrush.RadiusX = 0.7;
                eyeBrush.RadiusY = 0.7;

                // 眼睛边框 - 深绿色
                var borderColor = Color.FromRgb(46, 125, 50);
                var pen = new Pen(new SolidColorBrush(borderColor), Math.Max(1, size / 32.0));

                // 绘制眼睛椭圆
                dc.DrawEllipse(
                    eyeBrush,
                    pen,
                    new Point(centerX, centerY),
                    eyeWidth / 2,
                    eyeHeight / 2
                );

                // 瞳孔
                double pupilRadius = size * 0.15;
                var pupilCenter = new Point(centerX, centerY - size * 0.02);
                dc.DrawEllipse(
                    Brushes.DarkSlateGray,
                    null,
                    pupilCenter,
                    pupilRadius,
                    pupilRadius
                );

                // 瞳孔内部小圆
                dc.DrawEllipse(
                    Brushes.Gray,
                    null,
                    pupilCenter,
                    pupilRadius * 0.5,
                    pupilRadius * 0.5
                );

                // 高光
                var highlightCenter = new Point(
                    pupilCenter.X - size * 0.04,
                    pupilCenter.Y - size * 0.04
                );
                dc.DrawEllipse(
                    new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                    null,
                    highlightCenter,
                    size * 0.04,
                    size * 0.04
                );
            }

            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(drawingVisual);
            return bitmap;
        }

        static byte[] SavePngToBytes(BitmapSource bitmap)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using (var stream = new MemoryStream())
            {
                encoder.Save(stream);
                return stream.ToArray();
            }
        }

        static void CreateIcoFile(List<(int size, byte[] pngData)> bitmaps, string filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Create))
            using (var writer = new BinaryWriter(stream))
            {
                // ICO Header (6 bytes)
                writer.Write((short)0);      // Reserved
                writer.Write((short)1);      // Type: 1 = Icon
                writer.Write((short)bitmaps.Count);  // Count of images

                // Calculate offset to image data
                int headerSize = 6;
                int entrySize = 16;
                int offset = headerSize + (entrySize * bitmaps.Count);

                // Write ICONDIRENTRY for each image
                foreach (var (size, pngData) in bitmaps)
                {
                    writer.Write((byte)(size == 256 ? 0 : size));  // Width
                    writer.Write((byte)(size == 256 ? 0 : size));  // Height
                    writer.Write((byte)0);       // Colors (0 = >256)
                    writer.Write((byte)0);       // Reserved
                    writer.Write((short)1);      // Color planes
                    writer.Write((short)32);     // Bits per pixel
                    writer.Write(pngData.Length); // Size of image data
                    writer.Write(offset);        // Offset to image data

                    offset += pngData.Length;
                }

                // Write image data
                foreach (var (size, pngData) in bitmaps)
                {
                    writer.Write(pngData);
                }
            }

            Console.WriteLine($"  ICO文件: {filePath} ({bitmaps.Count} 个尺寸)");
        }
    }
}
