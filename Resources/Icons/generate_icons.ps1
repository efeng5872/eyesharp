# 图标生成脚本 - 使用 WPF 绘图功能生成护眼助手图标

Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName WindowsBase

function New-EyeIcon {
    param(
        [System.Windows.Media.Color]$EyeColor,
        [System.Windows.Media.Color]$PupilColor,
        [bool]$ShowPauseSymbol
    )

    $size = 64
    $scale = $size / 64.0

    $drawingVisual = New-Object System.Windows.Media.DrawingVisual
    $dc = $drawingVisual.RenderOpen()

    try {
        # 透明背景
        $dc.DrawRectangle(
            [System.Windows.Media.Brushes]::Transparent,
            $null,
            [System.Windows.Rect]::new(0, 0, $size, $size)
        )

        # 眼睛参数
        $eyeWidth = 48 * $scale
        $eyeHeight = 28 * $scale
        $centerX = $size / 2
        $centerY = $size / 2

        # 眼睛渐变
        $eyeBrush = New-Object System.Windows.Media.RadialGradientBrush(
            [System.Windows.Media.Color]::FromRgb(129, 199, 132),
            $EyeColor
        )
        $eyeBrush.Center = [System.Windows.Point]::new(0.5, 0.5)
        $eyeBrush.RadiusX = 0.7
        $eyeBrush.RadiusY = 0.7

        # 眼睛边框
        $borderBrush = New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromRgb(46, 125, 50))
        $pen = New-Object System.Windows.Media.Pen($borderBrush, 2)

        # 绘制眼睛椭圆
        $dc.DrawEllipse(
            $eyeBrush,
            $pen,
            [System.Windows.Point]::new($centerX, $centerY),
            $eyeWidth / 2,
            $eyeHeight / 2
        )

        # 瞳孔
        $pupilRadius = 10 * $scale
        $pupilCenter = [System.Windows.Point]::new($centerX, $centerY - 2 * $scale)
        $dc.DrawEllipse(
            [System.Windows.Media.Brushes]::DarkSlateGray,
            $null,
            $pupilCenter,
            $pupilRadius,
            $pupilRadius
        )

        # 瞳孔内小圆
        $dc.DrawEllipse(
            [System.Windows.Media.Brushes]::Gray,
            $null,
            $pupilCenter,
            $pupilRadius * 0.5,
            $pupilRadius * 0.5
        )

        # 高光
        $highlightCenter = [System.Windows.Point]::new(
            $pupilCenter.X - 3 * $scale,
            $pupilCenter.Y - 3 * $scale
        )
        $highlightBrush = New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromArgb(200, 255, 255, 255))
        $dc.DrawEllipse(
            $highlightBrush,
            $null,
            $highlightCenter,
            3 * $scale,
            3 * $scale
        )

        # 暂停角标
        if ($ShowPauseSymbol) {
            $badgeRadius = 14 * $scale
            $badgeCenter = [System.Windows.Point]::new($size - $badgeRadius - 2 * $scale, $size - $badgeRadius - 2 * $scale)

            # 红色圆形背景
            $badgeBrush = New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromRgb(244, 67, 54))
            $badgePen = New-Object System.Windows.Media.Pen([System.Windows.Media.Brushes]::White, 1.5)
            $dc.DrawEllipse($badgeBrush, $badgePen, $badgeCenter, $badgeRadius, $badgeRadius)

            # 暂停符号
            $barWidth = 3 * $scale
            $barHeight = 8 * $scale
            $gap = 2 * $scale

            # 左竖线
            $dc.DrawRectangle(
                [System.Windows.Media.Brushes]::White,
                $null,
                [System.Windows.Rect]::new(
                    $badgeCenter.X - $gap / 2 - $barWidth,
                    $badgeCenter.Y - $barHeight / 2,
                    $barWidth,
                    $barHeight
                )
            )

            # 右竖线
            $dc.DrawRectangle(
                [System.Windows.Media.Brushes]::White,
                $null,
                [System.Windows.Rect]::new(
                    $badgeCenter.X + $gap / 2,
                    $badgeCenter.Y - $barHeight / 2,
                    $barWidth,
                    $barHeight
                )
            )
        }
    }
    finally {
        $dc.Close()
    }

    $bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap(
        $size, $size,
        96, 96,
        [System.Windows.Media.PixelFormats]::Pbgra32
    )
    $bitmap.Render($drawingVisual)

    return $bitmap
}

function Save-Png {
    param([System.Windows.Media.Imaging.BitmapSource]$Bitmap, [string]$FilePath)

    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($Bitmap))

    $stream = [System.IO.FileStream]::new($FilePath, [System.IO.FileMode]::Create)
    try {
        $encoder.Save($stream)
    }
    finally {
        $stream.Close()
    }

    Write-Host "生成: $FilePath"
}

# 主程序
$outputDir = $PSScriptRoot

# 正常运行图标 - 绿色
Write-Host "生成正常运行图标 (绿色眼睛)..."
$normalIcon = New-EyeIcon `
    -EyeColor ([System.Windows.Media.Color]::FromRgb(76, 175, 80)) `
    -PupilColor ([System.Windows.Media.Color]::FromRgb(51, 51, 51)) `
    -ShowPauseSymbol $false

Save-Png -Bitmap $normalIcon -FilePath (Join-Path $outputDir "tray_normal.png")

# 暂停状态图标 - 橙色带暂停符号
Write-Host "生成暂停状态图标 (橙色眼睛带暂停符号)..."
$pausedIcon = New-EyeIcon `
    -EyeColor ([System.Windows.Media.Color]::FromRgb(255, 152, 0)) `
    -PupilColor ([System.Windows.Media.Color]::FromRgb(51, 51, 51)) `
    -ShowPauseSymbol $true

Save-Png -Bitmap $pausedIcon -FilePath (Join-Path $outputDir "tray_paused.png")

Write-Host "图标生成完成！"
Write-Host "文件位置: $outputDir"
