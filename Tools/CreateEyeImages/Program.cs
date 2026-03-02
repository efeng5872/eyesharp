using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CreateEyeImages
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            string outputDir = args.Length > 0 ? args[0] : "../../Resources/Images";

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            Console.WriteLine("生成护眼主题图片...");
            Console.WriteLine($"输出目录: {Path.GetFullPath(outputDir)}");

            // 生成5张不同色调的护眼图片
            GenerateImage(outputDir, "eye1.jpg", Colors.ForestGreen, Colors.DarkSeaGreen, "远眺");
            GenerateImage(outputDir, "eye2.jpg", Colors.SeaGreen, Colors.MediumSeaGreen, "草地");
            GenerateImage(outputDir, "eye3.jpg", Colors.DarkGreen, Colors.LightGreen, "森林");
            GenerateImage(outputDir, "eye4.jpg", Colors.Teal, Colors.MediumAquamarine, "湖泊");
            GenerateImage(outputDir, "eye5.jpg", Colors.OliveDrab, Colors.YellowGreen, "自然");

            Console.WriteLine("护眼图片生成完成!");
        }

        static void GenerateImage(string outputDir, string fileName, Color color1, Color color2, string theme)
        {
            const int width = 1920;
            const int height = 1080;

            var drawingVisual = new DrawingVisual();
            using (var dc = drawingVisual.RenderOpen())
            {
                // 渐变背景
                var gradientBrush = new LinearGradientBrush(color1, color2, 45);
                dc.DrawRectangle(gradientBrush, null, new Rect(0, 0, width, height));

                // 添加一些装饰性的圆形（模拟景深效果）
                var rand = new Random(theme.GetHashCode());
                for (int i = 0; i < 5; i++)
                {
                    double x = rand.NextDouble() * width;
                    double y = rand.NextDouble() * height;
                    double radius = 50 + rand.NextDouble() * 100;

                    var circleColor = Color.FromArgb(
                        (byte)(20 + rand.Next(30)),
                        (byte)(255 - color1.R),
                        (byte)(255 - color1.G),
                        (byte)(255 - color1.B));

                    dc.DrawEllipse(
                        new SolidColorBrush(circleColor),
                        null,
                        new Point(x, y),
                        radius,
                        radius
                    );
                }

                // 添加主题文字
                var text = new FormattedText(
                    $"护眼模式 - {theme}",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Microsoft YaHei"),
                    48,
                    Brushes.White,
                    96);

                dc.DrawText(text, new Point((width - text.Width) / 2, height - 100));

                // 添加中心提示文字
                var centerText = new FormattedText(
                    "让眼睛休息一下",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Microsoft YaHei"),
                    72,
                    new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                    96);

                dc.DrawText(centerText, new Point((width - centerText.Width) / 2, (height - centerText.Height) / 2));
            }

            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(drawingVisual);

            // 保存为JPG
            var encoder = new JpegBitmapEncoder();
            encoder.QualityLevel = 90;
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            string filePath = Path.Combine(outputDir, fileName);
            using (var stream = File.Create(filePath))
            {
                encoder.Save(stream);
            }

            Console.WriteLine($"  生成: {fileName} ({theme})");
        }
    }
}
