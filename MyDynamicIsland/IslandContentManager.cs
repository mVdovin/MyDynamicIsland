using MyDynamicIsland.Modules;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MyDynamicIsland
{
    /// <summary>
    /// Универсальный менеджер контента для Dynamic Island.
    /// </summary>
    public class IslandContentManager : INotifyPropertyChanged
    {
        private string _headerTitle = "Панель керування";
        private object _compactContent = "Активуйте необхідіні модулі.";
        private object? _expandedContent;

        public string HeaderTitle
        {
            get => _headerTitle;
            set { if (_headerTitle != value) { _headerTitle = value; OnPropertyChanged(); } }
        }

        public object CompactContent
        {
            get => _compactContent;
            set { if (_compactContent != value) { _compactContent = value; OnPropertyChanged(); } }
        }

        public object? ExpandedContent
        {
            get => _expandedContent;
            set { if (_expandedContent != value) { _expandedContent = value; OnPropertyChanged(); } }
        }

        public IslandContentManager()
        {
            LoadDefaultWidget();
        }

        /// <summary>
        /// Загружает тестовый виджет, который автоматически подстраивается под Светлую и Тёмную темы.
        /// </summary>
        public void LoadDefaultWidget()
        {
            HeaderTitle = "Налаштування";

            // 1. Создаем вертикальный контейнер для модулей
            var modulesContainer = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Vertical,
                Margin = new Thickness(0,0,0,0)
            };

            // 2. Добавляем первый модуль (Мониторинг системы)
            modulesContainer.Children.Add(new SystemMonitoringModule());

            // 3. Добавляем ТВОЙ НОВЫЙ МОДУЛЬ (UserControl1)
            modulesContainer.Children.Add(new UserControl1());

            // 4. Передаем весь контейнер в ExpandedContent – MainWindow сам всё отрисует!
            ExpandedContent = modulesContainer;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null!)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}