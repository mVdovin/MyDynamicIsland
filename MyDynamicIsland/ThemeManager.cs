using System.Windows;
using System.Windows.Media;

namespace MyDynamicIsland
{
    public enum IslandThemeType
    {
        Dark,
        Light
    }

    public class ThemeManager
    {
        private IslandThemeType _currentTheme = IslandThemeType.Dark;

        public IslandThemeType CurrentTheme => _currentTheme;

        /// <summary>
        /// Получает текущий системный акцентный цвет из настроек персонализации Windows.
        /// </summary>
        public System.Windows.Media.Color GetSystemAccentColor()
        {
            try
            {
                // SystemParameters.WindowGlassColor возвращает актуальный акцентный цвет Windows
                System.Windows.Media.Color accent = SystemParameters.WindowGlassColor;
                // Убеждаемся, что альфа-канал равен 255 (полная непрозрачность для элементов управления)
                return System.Windows.Media.Color.FromRgb(accent.R, accent.G, accent.B);
            }
            catch
            {
                // Резервный цвет (Fluent Blue), если система не вернула значение
                return System.Windows.Media.Color.FromRgb(0, 120, 212);
            }
        }

        /// <summary>
        /// Определяет текущую тему Windows (Тёмная или Светлая).
        /// </summary>
        public static IslandThemeType GetSystemTheme()
        {
            return Win32Helper.IsWindowsLightMode() ? IslandThemeType.Light : IslandThemeType.Dark;
        }

        /// <summary>
        /// Применяет выбранную тему и системный акцентный цвет к окну.
        /// </summary>
        public void ApplyTheme(Window window, IslandThemeType theme)
        {
            _currentTheme = theme;

            // Считываем реальный акцентный цвет Windows
            System.Windows.Media.Color systemAccent = GetSystemAccentColor();

            if (theme == IslandThemeType.Dark)
            {
                SetBrush(window, "SurfaceBackground", System.Windows.Media.Color.FromRgb(31, 31, 31));       // #1F1F1F
                SetBrush(window, "TextPrimary", System.Windows.Media.Color.FromRgb(255, 255, 255));          // #FFFFFF
                SetBrush(window, "TextSecondary", System.Windows.Media.Color.FromArgb(175, 255, 255, 255));  // Subtitle / Caption
                SetBrush(window, "CardBackground", System.Windows.Media.Color.FromArgb(18, 255, 255, 255));   // Фон карточек Fluent
                SetBrush(window, "CardBorder", System.Windows.Media.Color.FromArgb(25, 255, 255, 255));       // Тонкая рамка карточек
                SetBrush(window, "AccentPrimary", systemAccent);                        // Системный акцент Windows
            }
            else
            {
                SetBrush(window, "SurfaceBackground", System.Windows.Media.Color.FromRgb(245, 245, 247));    // #F5F5F7
                SetBrush(window, "TextPrimary", System.Windows.Media.Color.FromRgb(29, 29, 31));             // #1D1D1F
                SetBrush(window, "TextSecondary", System.Windows.Media.Color.FromArgb(225, 0, 0, 0));        // Subtitle / Caption
                SetBrush(window, "CardBackground", System.Windows.Media.Color.FromArgb(15, 0, 0, 0));        // Фон карточек Fluent
                SetBrush(window, "CardBorder", System.Windows.Media.Color.FromArgb(25, 0, 0, 0));            // Тонкая рамка карточек
                SetBrush(window, "AccentPrimary", systemAccent);                        // Системный акцент Windows
            }
        }

        public void ToggleTheme(Window window)
        {
            IslandThemeType nextTheme = _currentTheme == IslandThemeType.Dark ? IslandThemeType.Light : IslandThemeType.Dark;
            ApplyTheme(window, nextTheme);
        }

        private void SetBrush(Window window, string resourceKey, System.Windows.Media.Color color)
        {
            window.Resources[resourceKey] = new SolidColorBrush(color);
        }
    }
}