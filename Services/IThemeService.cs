using System;

namespace eyesharp.Services
{
    /// <summary>
    /// 主题类型
    /// </summary>
    public enum ThemeType
    {
        Light,
        Dark
    }

    /// <summary>
    /// 主题服务接口
    /// </summary>
    public interface IThemeService
    {
        /// <summary>
        /// 当前主题
        /// </summary>
        ThemeType CurrentTheme { get; }

        /// <summary>
        /// 主题变更事件
        /// </summary>
        event EventHandler<ThemeChangedEventArgs> ThemeChanged;

        /// <summary>
        /// 应用主题
        /// </summary>
        void ApplyTheme(ThemeType theme);

        /// <summary>
        /// 切换主题
        /// </summary>
        ThemeType ToggleTheme();

        /// <summary>
        /// 从字符串加载主题
        /// </summary>
        void LoadTheme(string themeName);
    }

    /// <summary>
    /// 主题变更事件参数
    /// </summary>
    public class ThemeChangedEventArgs : EventArgs
    {
        public ThemeType OldTheme { get; }
        public ThemeType NewTheme { get; }

        public ThemeChangedEventArgs(ThemeType oldTheme, ThemeType newTheme)
        {
            OldTheme = oldTheme;
            NewTheme = newTheme;
        }
    }
}
