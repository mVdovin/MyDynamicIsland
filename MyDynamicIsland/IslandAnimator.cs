using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MyDynamicIsland
{
    /// <summary>
    /// Класс, отвечающий за все анимации и плавные переходы Dynamic Island.
    /// </summary>
    public class IslandAnimator
    {
        // Стандартная функция плавности в стиле Apple / Fluent Design
        private readonly CubicEase _defaultEase = new CubicEase { EasingMode = EasingMode.EaseOut };

        /// <summary>
        /// Плавное изменение ширины и высоты элемента.
        /// </summary>
        /// <param name="targetElement">Элемент, размеры которого меняются (контейнер острова).</param>
        /// <param name="targetWidth">Целевая ширина в пикселях.</param>
        /// <param name="targetHeight">Целевая высота в пикселях.</param>
        /// <param name="duration">Время выполнения анимации.</param>
        public void AnimateSize(FrameworkElement targetElement, double targetWidth, double targetHeight, TimeSpan duration)
        {
            var widthAnim = new DoubleAnimation(targetWidth, duration) { EasingFunction = _defaultEase };
            var heightAnim = new DoubleAnimation(targetHeight, duration) { EasingFunction = _defaultEase };

            Timeline.SetDesiredFrameRate(widthAnim, 60);
            Timeline.SetDesiredFrameRate(heightAnim, 60);

            targetElement.BeginAnimation(FrameworkElement.WidthProperty, widthAnim);
            targetElement.BeginAnimation(FrameworkElement.HeightProperty, heightAnim);
        }

        /// <summary>
        /// Плавное масштабирование элемента (Hover-эффект).
        /// </summary>
        /// <param name="targetTransform">ScaleTransform, привязанный к элементу.</param>
        /// <param name="targetScale">Целевой масштаб (например, 1.05 для увеличения на 5%).</param>
        /// <param name="duration">Время выполнения анимации.</param>
        public void AnimateScale(ScaleTransform targetTransform, double targetScale, TimeSpan duration)
        {
            var scaleXAnim = new DoubleAnimation(targetScale, duration) { EasingFunction = _defaultEase };
            var scaleYAnim = new DoubleAnimation(targetScale, duration) { EasingFunction = _defaultEase };

            Timeline.SetDesiredFrameRate(scaleXAnim, 60);
            Timeline.SetDesiredFrameRate(scaleYAnim, 60);

            targetTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
            targetTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);
        }

        /// <summary>
        /// Анимация изменения свойства прилипания (SnapProgressProperty).
        /// </summary>
        /// <param name="targetElement">Окно или элемент, содержащий свойство.</param>
        /// <param name="progressProperty">DependencyProperty для анимации.</param>
        /// <param name="targetProgress">Целевое значение (1.0 — прилип, 0.0 — отвязан).</param>
        /// <param name="duration">Время выполнения анимации.</param>
        public void AnimateSnapProgress(UIElement targetElement, DependencyProperty progressProperty, double targetProgress, TimeSpan duration)
        {
            var progressAnim = new DoubleAnimation(targetProgress, duration) { EasingFunction = _defaultEase };
            Timeline.SetDesiredFrameRate(progressAnim, 60);

            targetElement.BeginAnimation(progressProperty, progressAnim);
        }

        /// <summary>
        /// Плавное переключение контента между компактным и развернутым режимом.
        /// </summary>
        /// <param name="compactContent">Контейнер компактного режима.</param>
        /// <param name="expandedContent">Контейнер расширенного режима.</param>
        /// <param name="showExpanded">True, если нужно показать расширенный режим; False для компактного.</param>
        /// <param name="duration">Время затухания и появления.</param>
        public void AnimateContentTransition(UIElement compactContent, UIElement expandedContent, bool showExpanded, TimeSpan duration)
        {
            if (showExpanded)
            {
                var fadeOut = new DoubleAnimation(0, duration);
                Timeline.SetDesiredFrameRate(fadeOut, 60);

                fadeOut.Completed += (s, e) =>
                {
                    compactContent.Visibility = Visibility.Collapsed;
                    expandedContent.Visibility = Visibility.Visible;

                    var fadeIn = new DoubleAnimation(1, duration);
                    Timeline.SetDesiredFrameRate(fadeIn, 60);
                    expandedContent.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                };
                compactContent.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
            else
            {
                var fadeOut = new DoubleAnimation(0, duration);
                Timeline.SetDesiredFrameRate(fadeOut, 60);

                fadeOut.Completed += (s, e) =>
                {
                    expandedContent.Visibility = Visibility.Collapsed;
                    compactContent.Visibility = Visibility.Visible;

                    // Возвращаем прозрачность компактного режима к 1
                    var fadeIn = new DoubleAnimation(1, duration);
                    Timeline.SetDesiredFrameRate(fadeIn, 60);
                    compactContent.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                };
                expandedContent.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
        }
    }
}