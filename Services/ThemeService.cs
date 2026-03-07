using System;
using System.Windows;
using System.Windows.Media;

namespace eyesharp.Services
{
    /// <summary>
    /// 主题服务实现
    /// </summary>
    public class ThemeService : IThemeService
    {
        private readonly ILogService _logService;
        private ThemeType _currentTheme = ThemeType.Light;

        public ThemeType CurrentTheme => _currentTheme;

        public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

        public ThemeService(ILogService logService)
        {
            _logService = logService;
        }

        /// <summary>
        /// 应用主题
        /// </summary>
        public void ApplyTheme(ThemeType theme)
        {
            if (_currentTheme == theme)
                return;

            var oldTheme = _currentTheme;
            _currentTheme = theme;

            _logService.Info($"应用主题: {theme}");

            // 更新应用程序资源
            UpdateApplicationResources(theme);

            // 触发主题变更事件
            ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(oldTheme, theme));
        }

        /// <summary>
        /// 切换主题（亮色 ↔ 暗色）
        /// </summary>
        public ThemeType ToggleTheme()
        {
            var newTheme = _currentTheme == ThemeType.Light ? ThemeType.Dark : ThemeType.Light;
            ApplyTheme(newTheme);
            return newTheme;
        }

        /// <summary>
        /// 从字符串加载主题
        /// </summary>
        public void LoadTheme(string themeName)
        {
            if (string.IsNullOrEmpty(themeName))
            {
                ApplyTheme(ThemeType.Light);
                return;
            }

            var theme = themeName.ToLower() switch
            {
                "dark" => ThemeType.Dark,
                "light" => ThemeType.Light,
                _ => ThemeType.Light
            };

            ApplyTheme(theme);
        }

        /// <summary>
        /// 更新应用程序资源
        /// </summary>
        private void UpdateApplicationResources(ThemeType theme)
        {
            if (Application.Current == null)
                return;

            var resources = Application.Current.Resources;

            if (theme == ThemeType.Dark)
            {
                // 暗色主题颜色
                resources["BackgroundBrush"] = new SolidColorBrush(Color.FromRgb(45, 45, 48));
                resources["CardBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(37, 37, 40));
                resources["TextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                resources["TextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(200, 200, 200));
                resources["TextDisabledBrush"] = new SolidColorBrush(Color.FromRgb(120, 120, 120));
                resources["BorderBrush"] = new SolidColorBrush(Color.FromRgb(80, 80, 85));
                resources["PrimaryBrush"] = new SolidColorBrush(Color.FromRgb(100, 181, 246));
                resources["PrimaryDarkBrush"] = new SolidColorBrush(Color.FromRgb(66, 165, 245));
                resources["PrimaryLightBrush"] = new SolidColorBrush(Color.FromRgb(144, 202, 249));
            }
            else
            {
                // 亮色主题颜色（恢复默认值）
                resources["BackgroundBrush"] = new SolidColorBrush(Color.FromRgb(250, 250, 250));
                resources["CardBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                resources["TextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(51, 51, 51));
                resources["TextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(102, 102, 102));
                resources["TextDisabledBrush"] = new SolidColorBrush(Color.FromRgb(153, 153, 153));
                resources["BorderBrush"] = new SolidColorBrush(Color.FromRgb(224, 224, 224));
                resources["PrimaryBrush"] = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                resources["PrimaryDarkBrush"] = new SolidColorBrush(Color.FromRgb(25, 118, 210));
                resources["PrimaryLightBrush"] = new SolidColorBrush(Color.FromRgb(100, 181, 246));
            }

            _logService.Info($"主题资源已更新: {theme}");
        }
    }
}
