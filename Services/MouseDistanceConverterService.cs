using System;
using System.Drawing;
using eyesharp.Models;

namespace eyesharp.Services
{
    /// <summary>
    /// 鼠标移动像素到物理距离换算服务
    /// </summary>
    public class MouseDistanceConverterService : IMouseDistanceConverterService
    {
        private const double InchesPerMeter = 39.37007874015748;
        private const double FallbackPixelsPerMeter = 3779.5;

        private readonly ILogService _logService;

        public MouseDistanceConverterService(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        public double ConvertPixelsToMeters(long pixels)
        {
            var config = App.CurrentConfig;
            var pixelsPerMeter = GetPixelsPerMeterInternal(config, out var source);
            var rawMeters = pixels / pixelsPerMeter;
            var calibratedMeters = rawMeters * config.MouseDistanceCalibrationFactor;

            _logService.Debug($"[MouseDistance] 像素换算: pixels={pixels}, ppm={pixelsPerMeter:F2}, factor={config.MouseDistanceCalibrationFactor:F3}, source={source}, meters={calibratedMeters:F4}");
            return calibratedMeters;
        }

        public double GetPixelsPerMeter()
        {
            var config = App.CurrentConfig;
            return GetPixelsPerMeterInternal(config, out _);
        }

        private double GetPixelsPerMeterInternal(AppConfig config, out string source)
        {
            if (config.MouseDistanceUseManualProfile)
            {
                var ppmByManual = CalculateManualPixelsPerMeter(config);
                source = "manual";
                return ppmByManual;
            }

            if (config.MouseDistanceUseAutoDpi && TryGetPrimaryScreenDpi(out var dpi))
            {
                source = "auto-dpi";
                return dpi * InchesPerMeter;
            }

            source = "fallback";
            return FallbackPixelsPerMeter;
        }

        private static double CalculateManualPixelsPerMeter(AppConfig config)
        {
            var scaledWidth = config.MouseDistanceManualResolutionWidth * (config.MouseDistanceManualScalePercent / 100.0);
            var scaledHeight = config.MouseDistanceManualResolutionHeight * (config.MouseDistanceManualScalePercent / 100.0);
            var diagonalPixels = Math.Sqrt((scaledWidth * scaledWidth) + (scaledHeight * scaledHeight));
            var ppi = diagonalPixels / config.MouseDistanceManualDiagonalInch;
            return ppi * InchesPerMeter;
        }

        private bool TryGetPrimaryScreenDpi(out double dpi)
        {
            try
            {
                using var graphics = Graphics.FromHwnd(IntPtr.Zero);
                dpi = graphics.DpiX;
                return dpi > 0;
            }
            catch (Exception ex)
            {
                _logService.Warn($"[MouseDistance] 自动DPI读取失败，降级兜底: {ex.Message}");
                dpi = 0;
                return false;
            }
        }
    }
}
