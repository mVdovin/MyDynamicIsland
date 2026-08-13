using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace MyDynamicIsland
{
    public static class ScrollHelper
    {
        #region 1. Анимируемое свойство смещения (Ваш оригинальный код)

        public static readonly DependencyProperty ScrollOffsetProperty =
            DependencyProperty.RegisterAttached(
                "ScrollOffset",
                typeof(double),
                typeof(ScrollHelper),
                new PropertyMetadata(0.0, (d, e) => (d as ScrollViewer)?.ScrollToVerticalOffset((double)e.NewValue)));

        public static void SetScrollOffset(DependencyObject target, double value) => target.SetValue(ScrollOffsetProperty, value);
        public static double GetScrollOffset(DependencyObject target) => (double)target.GetValue(ScrollOffsetProperty);

        #endregion

        #region 2. Хранение индивидуальной цели прокрутки (_targetOffset)

        private static readonly DependencyProperty TargetOffsetProperty =
            DependencyProperty.RegisterAttached(
                "TargetOffset",
                typeof(double),
                typeof(ScrollHelper),
                new PropertyMetadata(0.0));

        private static void SetTargetOffset(DependencyObject target, double value) => target.SetValue(TargetOffsetProperty, value);
        private static double GetTargetOffset(DependencyObject target) => (double)target.GetValue(TargetOffsetProperty);

        #endregion

        #region 3. Переключатель плавной прокрутки (EnableSmoothScroll)

        public static readonly DependencyProperty EnableSmoothScrollProperty =
            DependencyProperty.RegisterAttached(
                "EnableSmoothScroll",
                typeof(bool),
                typeof(ScrollHelper),
                new PropertyMetadata(false, OnEnableSmoothScrollChanged));

        public static void SetEnableSmoothScroll(DependencyObject target, bool value) => target.SetValue(EnableSmoothScrollProperty, value);
        public static bool GetEnableSmoothScroll(DependencyObject target) => (bool)target.GetValue(EnableSmoothScrollProperty);

        private static void OnEnableSmoothScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewer scrollViewer)
            {
                if ((bool)e.NewValue)
                {
                    scrollViewer.PreviewMouseWheel += ScrollViewer_PreviewMouseWheel;
                }
                else
                {
                    scrollViewer.PreviewMouseWheel -= ScrollViewer_PreviewMouseWheel;
                }
            }
        }

        #endregion

        #region 4. Универсальная логика плавного скролла

        private static void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer) return;

            e.Handled = true;

            // Считываем текущую цель для КОНКРЕТНОГО ScrollViewer
            double targetOffset = GetTargetOffset(scrollViewer);

            // Если скролл сместился вручную (например, зажатием бегунка), синхронизируем цель
            if (Math.Abs(targetOffset - scrollViewer.VerticalOffset) > 50)
            {
                targetOffset = scrollViewer.VerticalOffset;
            }

            // Накапливаем смещение (добавляем дельту к финальной цели)
            targetOffset -= e.Delta;

            // Ограничиваем рамками контента
            if (targetOffset < 0) targetOffset = 0;
            if (targetOffset > scrollViewer.ScrollableHeight) targetOffset = scrollViewer.ScrollableHeight;

            // Сохраняем новую цель обратно в ScrollViewer
            SetTargetOffset(scrollViewer, targetOffset);

            // Создаем и запускаем анимацию
            DoubleAnimation anim = new DoubleAnimation
            {
                To = targetOffset,
                Duration = TimeSpan.FromMilliseconds(600),
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
            };

            scrollViewer.BeginAnimation(ScrollOffsetProperty, anim);
        }

        #endregion
    }
}
