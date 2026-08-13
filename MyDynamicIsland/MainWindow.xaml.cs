using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace MyDynamicIsland
{
    public partial class MainWindow : Window
    {
        #region Win32 API: Системные события и позиционирование

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000; // Окно не отбирает фокус

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        // Константа события: смена окна на переднем плане
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;

        private delegate void WinEventDelegate(
            IntPtr hWinEventHook, uint eventType,
            IntPtr hwnd, int idObject, int idChild,
            uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(
            uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc, uint idProcess,
            uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        #endregion

        private IntPtr _hWinEventHook = IntPtr.Zero;
        private WinEventDelegate? _winEventProc;

        // Менеджер контента, отвечающий за текст и данные в свернутом и развернутом состояниях островка
        public IslandContentManager ContentManager { get; } = new IslandContentManager();
        public ThemeManager ThemeManager { get; } = new ThemeManager();
        public IslandAnimator Animator { get; } = new IslandAnimator();

        private bool _isExpanded = false;
        private bool _isDragging = false;
        private bool _isSnappedToTop = false;

        private Win32Helper.POINT _dragStartMousePos;
        private System.Windows.Point _windowStartPos;

        private const double SnapDistance = 25.0;
        private const double DetachThreshold = 28.0;
        private const double CompactHeight = 40.0;
        private const double ExpandedWidth = 360.0;
        private const double EarRadius = 14.0;
        private const double HoverScale = 1.031;

        private readonly TimeSpan _sizeDuration = TimeSpan.FromSeconds(0.3);
        private readonly TimeSpan _fadeDuration = TimeSpan.FromSeconds(0.15);
        private readonly TimeSpan _hoverDuration = TimeSpan.FromSeconds(0.15);
        private readonly TimeSpan _snapDuration = TimeSpan.FromSeconds(0.2);

        public static readonly DependencyProperty SnapProgressProperty =
            DependencyProperty.Register("SnapProgress", typeof(double), typeof(MainWindow),
            new PropertyMetadata(0.0, OnSnapProgressChanged));
        
        public double SnapProgress
        {
            get => (double)GetValue(SnapProgressProperty);
            set => SetValue(SnapProgressProperty, value);
        }

        private static void OnSnapProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((MainWindow)d).UpdateIslandGeometry();
        }

        public MainWindow()
        {
            InitializeComponent();

            this.DataContext = ContentManager;

            IslandThemeType systemTheme = ThemeManager.GetSystemTheme();
            ThemeManager.ApplyTheme(this, systemTheme);

            // Подписываемся на уведомления об изменении текста в ContentManager
            ContentManager.PropertyChanged += ContentManager_PropertyChanged;

            // При первичной загрузке окна
            Loaded += (s, e) => {
                IslandContainer.Width = GetCompactTargetWidth();
            };

            // Инициализируем иконку в трее при создании окна
            InitializeTrayIcon();
        }

        /// <summary>
        /// Вызывается при создании дескриптора окна. 
        /// Устанавливает стили и подписывается на системные уведомления ОС.
        /// </summary>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                // 1. Настраиваем окно как неактивируемый виджет поверх остальных окон
                int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                exStyle |= WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

                // 2. Сразу поднимаем на верхний слой
                BringIslandToTop(hwnd);

                // 3. Подключаем официальный хук: Windows сама сообщит, когда сменится активное окно
                _winEventProc = new WinEventDelegate(OnForegroundWindowChanged);
                _hWinEventHook = SetWinEventHook(
                    EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
                    IntPtr.Zero, _winEventProc, 0, 0, WINEVENT_OUTOFCONTEXT);
            }
        }

        /// <summary>
        /// Обработчик события от ОС: срабатывает строго в момент переключения фокуса или закрытия меню.
        /// </summary>
        private void OnForegroundWindowChanged(
            IntPtr hWinEventHook, uint eventType,
            IntPtr hwnd, int idObject, int idChild,
            uint dwEventThread, uint dwmsEventTime)
        {
            IntPtr myHwnd = new WindowInteropHelper(this).Handle;
            if (myHwnd != IntPtr.Zero && hwnd != myHwnd)
            {
                BringIslandToTop(myHwnd);
            }
        }

        /// <summary>
        /// Возвращает островок на вершину Z-порядка без перехвата фокуса.
        /// </summary>
        private void BringIslandToTop(IntPtr hwnd)
        {
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        /// <summary>
        /// Создает значок в системном трее с контекстным меню для закрытия приложения.
        /// </summary> pack://application:,,,/MyDynamicIsland;component/Image.ico
        private System.Windows.Forms.NotifyIcon? _notifyIcon;
        private TrayMenuWindow? _trayMenuWindow;

        private void InitializeTrayIcon()
        {
            // 1. Получаем поток данных иконки, вшитой в сборку приложения
            var iconUri = new Uri("pack://application:,,,/MyDynamicIsland;component/Image.ico", UriKind.RelativeOrAbsolute);
            var streamInfo = System.Windows.Application.GetResourceStream(iconUri);

            // 2. Инициализируем значок трея (с защитой на случай, если ресурс не найден)
            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = streamInfo != null
                    ? new System.Drawing.Icon(streamInfo.Stream)
                    : System.Drawing.SystemIcons.Application,
                Visible = true,
                Text = "Dynamic Island"
            };

            // Обрабатываем клики мышью по значку в трее
            _notifyIcon.MouseDown += (s, e) =>
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    // 1. По левому клику: выводим островок на передний план
                    BringIslandToFront();

                    // 2. И открываем нашу XAML-панельку над курсором
                    ToggleTrayMenuAtCursor();
                }
                else if (e.Button == System.Windows.Forms.MouseButtons.Right)
                {
                    // По правем клику: только открываем XAML-панельку
                    ToggleTrayMenuAtCursor();
                }
            };
        }

        /// <summary>
        /// Открывает XAML-меню строго над курсором мыши (возле иконки в трее).
        /// </summary>
        private IslandThemeType _lastTrayTheme = IslandThemeType.Dark;

        /// <summary>
        /// Гарантированно возвращает главное окно островка на передний план.
        /// </summary>
        private void BringIslandToFront()
        {
            // Если окно было скрыто — показываем его
            if (!this.IsVisible)
            {
                this.Show();
            }

            // Если окно было свернуто — разворачиваем
            if (this.WindowState == WindowState.Minimized)
            {
                this.WindowState = WindowState.Normal;
            }

            // Активируем окно в WPF
            this.Activate();
            this.Topmost = true;
            this.Focus();

            // Вызываем наш Win32-метод для поднятия на верхний системный Z-слой
            IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                BringIslandToTop(hwnd);
            }
        }

        /// <summary>
        /// Открывает или скрывает XAML-меню строго над курсором мыши.
        /// </summary>
        private void ToggleTrayMenuAtCursor()
        {
            IslandThemeType currentTheme = ThemeManager.CurrentTheme;

            // 1. Пересоздаем окно меню при смене темы для исключения мерцания
            if (_trayMenuWindow == null || _lastTrayTheme != currentTheme)
            {
                _trayMenuWindow?.Close();
                _trayMenuWindow = new TrayMenuWindow();
                _lastTrayTheme = currentTheme;
            }

            // 2. Если окно уже отображается — скрываем его
            if (_trayMenuWindow.IsVisible)
            {
                _trayMenuWindow.Hide();
            }
            else
            {
                // 3. Синхронизируем тему оформления
                _trayMenuWindow.SyncTheme(ThemeManager, currentTheme);

                // 4. Считываем физические координаты курсора мыши (в месте клика по трею)
                if (Win32Helper.GetCursorPos(out Win32Helper.POINT point))
                {
                    // Обновляем разметку окна для получения реальных размеров (Width / Height)
                    _trayMenuWindow.UpdateLayout();

                    double windowWidth = _trayMenuWindow.Width;
                    double windowHeight = _trayMenuWindow.Height;

                    if (double.IsNaN(windowWidth) || windowWidth == 0) windowWidth = _trayMenuWindow.ActualWidth;
                    if (double.IsNaN(windowHeight) || windowHeight == 0) windowHeight = _trayMenuWindow.ActualHeight;

                    // 5. Определяем монитор, на котором расположен КУРСОР
                    var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(point.X, point.Y));
                    var workArea = screen.WorkingArea; // Рабочая область без панели задач в пикселях

                    // 6. Получаем коэффициенты масштабирования (DPI)
                    var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(_trayMenuWindow);
                    double dpiScaleX = dpi.DpiScaleX;
                    double dpiScaleY = dpi.DpiScaleY;

                    // 7. Переводим физические пиксели в независимые единицы WPF (DIPs)
                    double cursorX = point.X / dpiScaleX;
                    double cursorY = point.Y / dpiScaleY;

                    double workAreaLeft = workArea.Left / dpiScaleX;
                    double workAreaRight = workArea.Right / dpiScaleX;
                    double workAreaTop = workArea.Top / dpiScaleY;

                    // 8. Центрируем окно над курсором мыши
                    double left = cursorX - (windowWidth / 2.0);
                    double top = cursorY - windowHeight - 10; // Отступ 10px вверх от иконки в трее

                    // 9. Корректируем координаты, если окно выходит за рамки текущего экрана
                    if (left + windowWidth > workAreaRight)
                    {
                        left = workAreaRight - windowWidth - 5;
                    }
                    if (left < workAreaLeft)
                    {
                        left = workAreaLeft + 5;
                    }
                    if (top < workAreaTop)
                    {
                        top = cursorY + 15; // Если панель задач расположена сверху экрана
                    }

                    // 10. Применяем рассчитанные координаты и отображаем окно
                    _trayMenuWindow.Left = left;
                    _trayMenuWindow.Top = top;

                    _trayMenuWindow.Show();
                    _trayMenuWindow.Activate();
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _trayMenuWindow?.Close();
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            base.OnClosed(e);
        }
        /// Безопасно вычисляет ширину свернутого островка, даже если контейнер сейчас скрыт.
        private double GetCompactTargetWidth()
        {
            var oldVisibility = CompactContent.Visibility;
            if (oldVisibility == Visibility.Collapsed)
            {
                // Hidden позволяет измерить реальные размеры без отрисовки элемента на экране
                CompactContent.Visibility = Visibility.Hidden;
            }

            CompactContent.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));

            // DesiredSize уже учитывает Margin контейнера, добавляем запас 24px для скруглений
            double calculatedWidth = CompactContent.DesiredSize.Width + 24;

            CompactContent.Visibility = oldVisibility;

            return Math.Max(calculatedWidth, 40);
        }

        /// Срабатывает каждый раз, когда менеджер контента обновляет текст в свернутом островке (CompactContent).
        /// Гарантированно ловит момент подгрузки данных мониторинга и расширяет капсулу.
        private void ContentManager_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Проверяем, что изменился именно текст свернутого режима
            if (e.PropertyName == nameof(ContentManager.CompactContent))
            {
                // Работаем только если островок свернут
                if (!_isExpanded)
                {
                    // Вычисляем реальную целевую ширину под новый длинный текст
                    double targetWidth = GetCompactTargetWidth();

                    // Если ширина отличается, плавно анимируем капсулу
                    if (Math.Abs(IslandContainer.Width - targetWidth) > 15.0)
                    {
                        Animator.AnimateSize(IslandContainer, targetWidth, CompactHeight, _sizeDuration);
                    }
                }
            }
        }

        /// Безопасно вычисляет высоту развернутого островка под любой вложенный контент без лишних пустых зон снизу.
        private double GetExpandedTargetHeight()
        {
            var oldVisibility = ExpandedContent.Visibility;
            if (oldVisibility == Visibility.Collapsed)
            {
                // Hidden позволяет измерить реальные размеры элемента без отображения на экране
                ExpandedContent.Visibility = Visibility.Hidden;
            }

            // Измеряем контент при фиксированной ширине ExpandedWidth (360px)
            ExpandedContent.Measure(new System.Windows.Size(ExpandedWidth, double.PositiveInfinity));

            // DesiredSize.Height УЖЕ включает в себя собственный Margin контейнера ExpandedContent!
            // Поэтому прибавлять дополнительные пиксели не нужно.
            double calculatedHeight = ExpandedContent.DesiredSize.Height;

            ExpandedContent.Visibility = oldVisibility;

            // Возвращаем рассчитанную высоту (но не менее 70px для безопасности)
            return Math.Max(calculatedHeight, 70);
        }

        #region Векторная геометрия острова

        private void UpdateIslandGeometry()
        {
            double w = IslandContainer.ActualWidth;
            double h = IslandContainer.ActualHeight;

            if (w <= 0 || h <= 0) return;

            PathGeometry geometry = new PathGeometry();
            PathFigure figure = new PathFigure();

            ContentGrid.Margin = new Thickness(0);

            double R = _isExpanded ? 12.0 : (h / 2.0);
            double E = EarRadius;
            double p = SnapProgress;

            double tlStartX = R * (1.0 - p) - E * p;
            double tlC1X = (R * 0.448) * (1.0 - p) - (E * 0.45) * p;
            double tlC2Y = (R * 0.448) * (1.0 - p) + (E * 0.45) * p;
            double tlEndY = R * (1.0 - p) + E * p;

            figure.StartPoint = new System.Windows.Point(tlStartX, 0);

            figure.Segments.Add(new BezierSegment(
                new System.Windows.Point(tlC1X, 0),
                new System.Windows.Point(0, tlC2Y),
                new System.Windows.Point(0, tlEndY),
                true));

            figure.Segments.Add(new LineSegment(new System.Windows.Point(0, h - R), true));

            figure.Segments.Add(new ArcSegment(
                new System.Windows.Point(R, h),
                new System.Windows.Size(R, R),
                0, false, SweepDirection.Counterclockwise, true));

            figure.Segments.Add(new LineSegment(new System.Windows.Point(w - R, h), true));

            figure.Segments.Add(new ArcSegment(
                new System.Windows.Point(w, h - R),
                new System.Windows.Size(R, R),
                0, false, SweepDirection.Counterclockwise, true));

            double trStartY = R * (1.0 - p) + E * p;
            figure.Segments.Add(new LineSegment(new System.Windows.Point(w, trStartY), true));

            double trC1Y = (R * 0.448) * (1.0 - p) + (E * 0.45) * p;
            double trC2X = w - (R * 0.448) * (1.0 - p) + (E * 0.45) * p;
            double trEndX = w - R * (1.0 - p) + E * p;

            figure.Segments.Add(new BezierSegment(
                new System.Windows.Point(w, trC1Y),
                new System.Windows.Point(trC2X, 0),
                new System.Windows.Point(trEndX, 0),
                true));

            figure.Segments.Add(new LineSegment(new System.Windows.Point(tlStartX, 0), true));

            figure.IsClosed = true;
            geometry.Figures.Add(figure);
            IslandPath.Data = geometry;
        }

        private void IslandContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateIslandGeometry();
        }

        #endregion

        #region Перетаскивание и физика прилипания

        private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Win32Helper.GetCursorPos(out _dragStartMousePos);
            _windowStartPos = new System.Windows.Point(this.Left, this.Top);
            _isDragging = false;

            Mouse.Capture(this, CaptureMode.SubTree);
        }

        private void Window_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && IsMouseCaptured)
            {
                Win32Helper.GetCursorPos(out Win32Helper.POINT currentMousePos);
                DpiScale dpi = VisualTreeHelper.GetDpi(this);

                double deltaX = (currentMousePos.X - _dragStartMousePos.X) / dpi.DpiScaleX;
                double deltaY = (currentMousePos.Y - _dragStartMousePos.Y) / dpi.DpiScaleY;

                if (!_isDragging && (Math.Abs(deltaX) > 3 || Math.Abs(deltaY) > 3))
                {
                    _isDragging = true;
                }

                if (_isDragging)
                {
                    Rect screenArea = Win32Helper.GetCurrentScreenWorkAreaDip(this);

                    if (_isSnappedToTop)
                    {
                        if (deltaY > DetachThreshold)
                        {
                            UnsnapFromTop();

                            this.Top = screenArea.Top + 2;
                            this.Left = _windowStartPos.X + deltaX;

                            _dragStartMousePos = currentMousePos;
                            _windowStartPos = new System.Windows.Point(this.Left, this.Top);
                        }
                        else
                        {
                            this.Left = _windowStartPos.X + deltaX;
                            this.Top = screenArea.Top;
                        }
                        return;
                    }

                    this.Left = _windowStartPos.X + deltaX;
                    this.Top = _windowStartPos.Y + deltaY;

                    if (deltaY < 0 && Math.Abs(this.Top - screenArea.Top) < SnapDistance)
                    {
                        SnapToTop(screenArea.Top);
                    }
                }
            }
        }

        private void Window_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (IsMouseCaptured)
            {
                Mouse.Capture(null);
            }

            if (!_isDragging && !_isExpanded)
            {
                ToggleExpandState();
            }
            else
            {
                _isDragging = false;
            }
        }

        private void SnapToTop(double screenTop)
        {
            if (_isSnappedToTop) return;
            _isSnappedToTop = true;
            this.Top = screenTop;

            IslandShadow.Direction = 270;
            IslandShadow.ShadowDepth = 3;
            IslandShadow.BlurRadius = 8;

            Animator.AnimateSnapProgress(this, SnapProgressProperty, 1.0, _snapDuration);
        }

        private void UnsnapFromTop()
        {
            if (!_isSnappedToTop) return;
            _isSnappedToTop = false;

            IslandShadow.Direction = 270;
            IslandShadow.ShadowDepth = 4;
            IslandShadow.BlurRadius = 12;

            Animator.AnimateSnapProgress(this, SnapProgressProperty, 0.0, _snapDuration);
        }

        #endregion

        #region Анимация и кнопки управления

        /// <summary>
        /// Публичный метод для динамического пересчета и анимации высоты островка.
        /// Вызывается из дочерних модулей (например, при раскрытии спойлера в SystemMonitoringModule).
        /// </summary>
        public void AnimateIslandResize()
        {
            // Анимируем высоту только если островок сейчас находится в развернутом состоянии
            if (!_isExpanded) return;

            // 1. Принудительно обновляем разметку, чтобы WPF учесть появившийся или скрытый блок спойлера
            this.UpdateLayout();

            // 2. Вычисляем новую целевую высоту с помощью твоего штатного метода измерения
            double newTargetHeight = GetExpandedTargetHeight();

            // 3. Запускаем твою плавную анимацию изменения высоты (ширина остается фиксированной: ExpandedWidth)
            Animator.AnimateSize(IslandContainer, ExpandedWidth, newTargetHeight, _sizeDuration);
        }

        private void ToggleExpandState()
        {
            _isExpanded = !_isExpanded;

            // СБРАСЫВАЕМ МАСШТАБ ФОНА ПРИ РАЗВОРАЧИВАНИИ (1.05 -> 1.0)
            // Иначе фон развернутого окна остается растянутым по высоте на 5%
            // и вылезает снизу той самой пустой полосой!
            if (_isExpanded)
            {
                Animator.AnimateScale(IslandScale, 1.0, _hoverDuration);
            }

            double targetWidth = _isExpanded ? ExpandedWidth : GetCompactTargetWidth();
            double targetHeight = _isExpanded ? GetExpandedTargetHeight() : CompactHeight;

            Animator.AnimateSize(IslandContainer, targetWidth, targetHeight, _sizeDuration);
            Animator.AnimateContentTransition(CompactContent, ExpandedContent, _isExpanded, _fadeDuration);
        }

        private void IslandContainer_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isExpanded && !_isDragging)
            {
                Animator.AnimateScale(IslandScale, HoverScale, _hoverDuration);
            }
        }

        private void IslandContainer_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isExpanded && !_isDragging)
            {
                Animator.AnimateScale(IslandScale, 1.0, _hoverDuration);
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isExpanded)
            {
                ToggleExpandState();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.ToggleTheme(this);
        }

        #endregion
    }
}