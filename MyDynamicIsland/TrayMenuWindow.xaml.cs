using System;
using System.Windows;
using System.Windows.Threading;

namespace MyDynamicIsland
{
    /// <summary>
    /// Окно меню трея с автоматическим скрытием при уходе курсора мыши.
    /// </summary>
    public partial class TrayMenuWindow : Window
    {
        // Таймер для отслеживания ухода курсора мыши
        private readonly DispatcherTimer _mouseTrackerTimer;

        public TrayMenuWindow()
        {
            InitializeComponent();

            // Инициализируем таймер проверки позиции мыши (срабатывает 5 раз в секунду)
            _mouseTrackerTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _mouseTrackerTimer.Tick += MouseTrackerTimer_Tick;

            // Подписываемся на события показа и скрытия окна
            this.IsVisibleChanged += TrayMenuWindow_IsVisibleChanged;
        }

        /// <summary>
        /// Включает таймер только тогда, когда окно реально отображается на экране.
        /// </summary>
        private void TrayMenuWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (this.IsVisible)
            {
                _mouseTrackerTimer.Start();
            }
            else
            {
                _mouseTrackerTimer.Stop();
            }
        }

        /// <summary>
        /// Автоматически скрывает окно, если курсор мыши ушел далеко за его пределы.
        /// </summary>
        private void MouseTrackerTimer_Tick(object? sender, EventArgs e)
        {
            if (!this.IsVisible) return;

            // Получаем текущие координаты курсора через твой Win32Helper
            if (Win32Helper.GetCursorPos(out Win32Helper.POINT point))
            {
                var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
                double cursorX = point.X / dpi.DpiScaleX;
                double cursorY = point.Y / dpi.DpiScaleY;

                // Границы нашего окна с небольшим запасом в 60 пикселей
                double margin = 40;
                double leftBound = this.Left - margin;
                double rightBound = this.Left + this.Width + margin;
                double topBound = this.Top - margin;
                double bottomBound = this.Top + this.Height + margin;

                // Если курсор мыши вышел за пределы этой зоны — скрываем окно
                if (cursorX < leftBound || cursorX > rightBound ||
                    cursorY < topBound || cursorY > bottomBound)
                {
                    this.Hide();
                }
            }
        }

        /// <summary>
        /// Применяет тему с помощью твоего ThemeManager.
        /// </summary>
        public void SyncTheme(ThemeManager themeManager, IslandThemeType theme)
        {
            this.Opacity = 0;
            themeManager.ApplyTheme(this, theme);
            this.UpdateLayout();
            this.Opacity = 1;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            this.Hide();
        }
    }
}