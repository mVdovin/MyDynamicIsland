using LibreHardwareMonitor.Hardware;
using Microsoft.Win32;
using MyDynamicIsland.Modules;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MyDynamicIsland
{
    public partial class SystemMonitoringModule : System.Windows.Controls.UserControl
    {
        private readonly DispatcherTimer _monitoringTimer = new DispatcherTimer();
        private UniversalHardwareEngine? _engine;
        private bool _isBusy = false;

        // Флаг раскрытия/скрытия блока настроек (Спойлер)
        private bool _isExpanded = false;

        public ObservableCollection<DiskMonitoringItem> DisksList { get; set; } = new ObservableCollection<DiskMonitoringItem>();
        public ObservableCollection<NetworkMonitoringItem> NetworksList { get; set; } = new ObservableCollection<NetworkMonitoringItem>();

        public SystemMonitoringModule()
        {
            InitializeComponent();
            DisksItemsControl.ItemsSource = DisksList;
            NetworksItemsControl.ItemsSource = NetworksList;

            _monitoringTimer.Interval = TimeSpan.FromSeconds(1);
            _monitoringTimer.Tick += MonitoringTimer_Tick;

            Loaded += SystemMonitoringModule_Loaded;
            Unloaded += SystemMonitoringModule_Unloaded;
        }

        private async void SystemMonitoringModule_Loaded(object sender, RoutedEventArgs e)
        {
            if (_engine == null)
            {
                _engine = new UniversalHardwareEngine();
            }

            // Загружаем списки дисков и сетей асинхронно
            await RefreshHardwareListsAsync();

            if (MainMonitoringToggle.IsChecked == true)
            {
                StartMonitoring();
            }
        }

        private void SystemMonitoringModule_Unloaded(object sender, RoutedEventArgs e)
        {
            StopMonitoring();
            _engine?.Dispose();
            _engine = null;
        }

        private IslandContentManager? GetContentManager()
        {
            return System.Windows.Application.Current.MainWindow?.DataContext as IslandContentManager;
        }

        #region Управление спойлером (Раскрытие / Скрытие настроек)

        /// <summary>
        /// Обработчик клика по шапке: открывает или скрывает контейнер настроек
        /// и запускает штатную анимацию высоты в MainWindow.
        /// </summary>
        private void HeaderCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source && IsChildOf(source, MainMonitoringToggle))
            {
                return;
            }

            _isExpanded = !_isExpanded;
            SettingsContainer.Visibility = _isExpanded ? Visibility.Visible : Visibility.Collapsed;

            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                mainWindow.AnimateIslandResize();
            }
        }

        private static bool IsChildOf(DependencyObject child, DependencyObject parent)
        {
            while (child != null)
            {
                if (child == parent) return true;
                child = VisualTreeHelper.GetParent(child);
            }
            return false;
        }

        #endregion

        private void MainMonitoringToggle_Checked(object sender, RoutedEventArgs e) => StartMonitoring();
        private void MainMonitoringToggle_Unchecked(object sender, RoutedEventArgs e) => StopMonitoring();

        private void StartMonitoring()
        {
            var manager = GetContentManager();
            if (manager != null) manager.CompactContent = "Система: читання...";
            _monitoringTimer?.Start();
        }

        private void StopMonitoring()
        {
            _monitoringTimer?.Stop();
            var manager = GetContentManager();
            if (manager != null)
            {
                manager.CompactContent = "Моніторинг вимкнено";
                CpuText.Text = "Вимкнено користувачем";
                GpuText.Text = "Вимкнено користувачем";
                RamText.Text = "Вимкнено користувачем";
            }
        }

        private async void MonitoringTimer_Tick(object? sender, EventArgs e)
        {
            var manager = GetContentManager();
            if (manager == null || _engine == null || _isBusy) return;

            try
            {
                _isBusy = true;

                var (stats, disksData, netData) = await Task.Run(() =>
                {
                    HardwareStats currentStats = _engine.GetLatestStats();
                    List<DiskRawData> currentDisks = _engine.GetDisksRawData();
                    List<NetworkRawData> currentNets = _engine.GetNetworksRawData();
                    return (currentStats, currentDisks, currentNets);
                });

                var compactParts = new List<string>();

                // 1. Процессор (CPU - фиксируем 3 символа под проценты)
                if (CpuToggle.IsChecked == true)
                {
                    CpuText.Text = $"CPU: {stats.CpuLoad}% | {stats.CpuFreqGhz:F2} ГГц";
                    compactParts.Add($"CPU:{stats.CpuLoad,2}% • {stats.CpuFreqGhz,3:F2} ГГц");
                }
                else CpuText.Text = "Вимкнено користувачем";

                // 2. Видеокарта (GPU)
                if (GpuToggle.IsChecked == true)
                {
                    string gpuTempStr = stats.GpuTemp > 0 ? $"{stats.GpuTemp}°C" : "--°C";
                    GpuText.Text = $"GPU: {stats.GpuLoad}% | {stats.GpuFreqGhz:F2} ГГц | {gpuTempStr}";
                    compactParts.Add($"GPU:{stats.GpuLoad,2}% • {gpuTempStr,3}");
                }
                else GpuText.Text = "Вимкнено користувачем";

                // 3. Оперативная память (RAM)
                if (RamToggle.IsChecked == true)
                {
                    RamText.Text = $"RAM: {stats.RamUsedGb} ГБ / {stats.RamTotalGb} ГБ ({stats.RamLoadPercent}%)";
                    compactParts.Add($"RAM:{stats.RamLoadPercent,2}% • {stats.RamUsedGb,2} ГБ");
                }
                else RamText.Text = "Вимкнено користувачем";

                // 4. Диски
                ApplyDisksDataToUI(disksData);
                foreach (var disk in DisksList)
                {
                    if (disk.IsSelectedForCompact)
                    {
                        string hoursText = disk.PowerOnHours > 0 ? $"{disk.PowerOnHours} год." : "N/A";
                        compactParts.Add($"{disk.DriveLetter}{disk.UsedPercent,2}% • {(disk.TotalGb - disk.UsedGb),3} ГБ");
                    }
                }

                // 5. Сети
                ApplyNetworksDataToUI(netData);
                foreach (var net in NetworksList)
                {
                    if (net.IsSelectedForCompact)
                    {
                        compactParts.Add(net.CompactText);
                    }
                }

                manager.CompactContent = compactParts.Count > 0
                    ? string.Join(" | ", compactParts)
                    : "Усі датчики вимкнено";
            }
            catch
            {
                manager.CompactContent = "Помилка читання датчиків";
            }
            finally
            {
                _isBusy = false;
            }
        }

        private async Task RefreshHardwareListsAsync()
        {
            if (_engine == null) return;
            var (disksData, netData) = await Task.Run(() => (_engine.GetDisksRawData(), _engine.GetNetworksRawData()));
            ApplyDisksDataToUI(disksData);
            ApplyNetworksDataToUI(netData);
        }

        private void ApplyDisksDataToUI(List<DiskRawData> disksData)
        {
            foreach (var raw in disksData)
            {
                var existingItem = DisksList.FirstOrDefault(d => d.DriveLetter == raw.DriveLetter);
                if (existingItem == null)
                {
                    bool selectDefault = raw.DriveLetter?.Equals("C:", StringComparison.OrdinalIgnoreCase) == true;
                    DisksList.Add(new DiskMonitoringItem
                    {
                        DriveLetter = raw.DriveLetter,
                        UsedGb = raw.UsedGb,
                        TotalGb = raw.TotalGb,
                        UsedPercent = raw.UsedPercent,
                        PowerOnHours = raw.PowerOnHours,
                        IsSelectedForCompact = selectDefault
                    });
                }
                else
                {
                    existingItem.UsedGb = raw.UsedGb;
                    existingItem.TotalGb = raw.TotalGb;
                    existingItem.UsedPercent = raw.UsedPercent;
                    existingItem.PowerOnHours = raw.PowerOnHours;
                    existingItem.RefreshDisplay();
                }
            }
        }

        private void ApplyNetworksDataToUI(List<NetworkRawData> netData)
        {
            foreach (var raw in netData)
            {
                var existingItem = NetworksList.FirstOrDefault(n => n.InterfaceName == raw.InterfaceName);
                if (existingItem == null)
                {
                    bool selectDefault = raw.InterfaceName?.IndexOf("Wi-Fi", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                         raw.InterfaceName?.IndexOf("Ethernet", StringComparison.OrdinalIgnoreCase) >= 0;
                    NetworksList.Add(new NetworkMonitoringItem
                    {
                        InterfaceName = raw.InterfaceName,
                        DownloadBytesPerSec = raw.DownloadSpeedBytesPerSec,
                        UploadBytesPerSec = raw.UploadSpeedBytesPerSec,
                        IsSelectedForCompact = selectDefault
                    });
                }
                else
                {
                    existingItem.DownloadBytesPerSec = raw.DownloadSpeedBytesPerSec;
                    existingItem.UploadBytesPerSec = raw.UploadSpeedBytesPerSec;
                    existingItem.RefreshDisplay();
                }
            }
        }
    }

    #region Модели данных дисков и сети
    public class DiskRawData
    {
        public string? DriveLetter { get; set; }
        public long UsedGb { get; set; }
        public long TotalGb { get; set; }
        public int UsedPercent { get; set; }
        public int PowerOnHours { get; set; }
    }

    public class DiskMonitoringItem : System.ComponentModel.INotifyPropertyChanged
    {
        public string? DriveLetter { get; set; }
        public long UsedGb { get; set; }
        public long TotalGb { get; set; }
        public int UsedPercent { get; set; }
        public int PowerOnHours { get; set; }

        private bool _isSelectedForCompact;
        public bool IsSelectedForCompact
        {
            get => _isSelectedForCompact;
            set
            {
                if (_isSelectedForCompact != value)
                {
                    _isSelectedForCompact = value;
                    OnPropertyChanged(nameof(IsSelectedForCompact));
                }
            }
        }

        public string DisplayTitle => $"Диск {DriveLetter}";
        public string DisplayDetails => $"Зайнято: {UsedGb} ГБ з {TotalGb} ГБ ({UsedPercent}%)\nНапрацювання: {(PowerOnHours > 0 ? $"{PowerOnHours} год." : "N/A")}";

        public void RefreshDisplay() => OnPropertyChanged(nameof(DisplayDetails));

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propName) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propName));
    }

    public class NetworkRawData
    {
        public string? InterfaceName { get; set; }
        public double DownloadSpeedBytesPerSec { get; set; }
        public double UploadSpeedBytesPerSec { get; set; }
    }

    public class NetworkMonitoringItem : System.ComponentModel.INotifyPropertyChanged
    {
        public string? InterfaceName { get; set; }
        public double DownloadBytesPerSec { get; set; }
        public double UploadBytesPerSec { get; set; }

        private bool _isSelectedForCompact;
        public bool IsSelectedForCompact
        {
            get => _isSelectedForCompact;
            set
            {
                if (_isSelectedForCompact != value)
                {
                    _isSelectedForCompact = value;
                    OnPropertyChanged(nameof(IsSelectedForCompact));
                }
            }
        }

        public string? DisplayTitle => InterfaceName;
        public string DisplayDetails => $"↓{FormatSpeed(DownloadBytesPerSec)} • ↑{FormatSpeed(UploadBytesPerSec)}";
        public string CompactText => $"↓{FormatSpeed(DownloadBytesPerSec)} • ↑{FormatSpeed(UploadBytesPerSec)}";

        public static string FormatSpeed(double bytesPerSec)
        {
            if (bytesPerSec < 1024)
            {
                return $"{"0,00",6} КБ/с";
            }

            double kbPerSec = bytesPerSec / 1024.0;
            if (kbPerSec < 1024)
            {
                return $"{kbPerSec,6:F2} КБ/с";
            }

            double mbPerSec = kbPerSec / 1024.0;
            return $"{mbPerSec,6:F2} МБ/с";
        }

        public void RefreshDisplay() => OnPropertyChanged(nameof(DisplayDetails));

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propName) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propName));
    }
    #endregion

    #region Автономный движок мониторинга (Stable Base + Isolated CPU)
    public class HardwareStats
    {
        public int CpuLoad { get; set; }
        public double CpuFreqGhz { get; set; }
        public int GpuLoad { get; set; }
        public int GpuTemp { get; set; }
        public double GpuFreqGhz { get; set; }
        public double RamUsedGb { get; set; }
        public double RamTotalGb { get; set; }
        public int RamLoadPercent { get; set; }
    }

    public class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);
        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (IHardware subHardware in hardware.SubHardware)
            {
                subHardware.Accept(this);
            }
        }
        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }

    public class UniversalHardwareEngine : IDisposable
    {
        private readonly Computer _computer;
        private readonly UpdateVisitor _updateVisitor = new UpdateVisitor();
        private readonly CpuMonitorEngine _cpuEngine;

        private readonly Dictionary<string, (long rx, long tx, DateTime time)> _prevNetworkStats =
            new Dictionary<string, (long rx, long tx, DateTime time)>();

        public UniversalHardwareEngine()
        {
            _cpuEngine = new CpuMonitorEngine();

            _computer = new Computer
            {
                IsCpuEnabled = false, // Процессор вынесен в отдельный изолированный класс
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsStorageEnabled = true,
                IsMotherboardEnabled = false,
                IsControllerEnabled = false,
                IsNetworkEnabled = false
            };

            try
            {
                _computer.Open();
                _computer.Accept(_updateVisitor);
            }
            catch { }
        }

        #region Мониторинг дисков и сети
        public List<DiskRawData> GetDisksRawData()
        {
            var result = new List<DiskRawData>();
            try
            {
                var drives = DriveInfo.GetDrives();
                foreach (var drive in drives)
                {
                    if (!drive.IsReady || drive.DriveType != DriveType.Fixed) continue;
                    string driveLetter = drive.Name.TrimEnd('\\');
                    long totalGb = drive.TotalSize / (1024 * 1024 * 1024);
                    long freeGb = drive.TotalFreeSpace / (1024 * 1024 * 1024);
                    long usedGb = totalGb - freeGb;
                    int usedPercent = totalGb > 0 ? (int)((usedGb * 100) / totalGb) : 0;

                    result.Add(new DiskRawData
                    {
                        DriveLetter = driveLetter,
                        UsedGb = usedGb,
                        TotalGb = totalGb,
                        UsedPercent = usedPercent
                    });
                }
            }
            catch { }
            return result;
        }

        public List<NetworkRawData> GetNetworksRawData()
        {
            var result = new List<NetworkRawData>();
            try
            {
                DateTime now = DateTime.UtcNow;
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();

                foreach (var ni in interfaces)
                {
                    if (ni.OperationalStatus != OperationalStatus.Up ||
                        ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    {
                        continue;
                    }

                    var stats = ni.GetIPStatistics();
                    long currentRx = stats.BytesReceived;
                    long currentTx = stats.BytesSent;

                    double rxSpeed = 0;
                    double txSpeed = 0;

                    if (_prevNetworkStats.TryGetValue(ni.Name, out var prev))
                    {
                        double seconds = (now - prev.time).TotalSeconds;
                        if (seconds > 0)
                        {
                            rxSpeed = Math.Max(0, (currentRx - prev.rx) / seconds);
                            txSpeed = Math.Max(0, (currentTx - prev.tx) / seconds);
                        }
                    }

                    _prevNetworkStats[ni.Name] = (currentRx, currentTx, now);

                    if (rxSpeed > 1024 || txSpeed > 1024 || ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                    {
                        result.Add(new NetworkRawData
                        {
                            InterfaceName = ni.Name,
                            DownloadSpeedBytesPerSec = rxSpeed,
                            UploadSpeedBytesPerSec = txSpeed
                        });
                    }
                }
            }
            catch { }
            return result;
        }
        #endregion

        public HardwareStats GetLatestStats()
        {
            var stats = new HardwareStats
            {
                // Получаем данные CPU из изолированного класса
                CpuLoad = _cpuEngine.GetCpuUsagePercent(),
                CpuFreqGhz = _cpuEngine.GetCpuFrequencyGhz()
            };

            try
            {
                _computer.Accept(_updateVisitor);

                foreach (IHardware hardware in _computer.Hardware)
                {
                    ScanHardwareRecursive(hardware, stats);
                }
            }
            catch { }

            return stats;
        }

        private void ScanHardwareRecursive(IHardware hardware, HardwareStats stats)
        {
            // Видеокарты (NVIDIA / AMD / Intel)
            if (hardware.HardwareType == HardwareType.GpuNvidia ||
                hardware.HardwareType == HardwareType.GpuAmd ||
                hardware.HardwareType == HardwareType.GpuIntel)
            {
                foreach (ISensor sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Load && sensor.Value.HasValue)
                    {
                        string name = sensor.Name.ToLower();
                        if (name.Contains("core") || name.Contains("d3d") || name.Contains("gpu usage"))
                        {
                            int load = (int)Math.Round(sensor.Value.Value);
                            if (load > stats.GpuLoad) stats.GpuLoad = load;
                        }
                    }

                    if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue && sensor.Value.Value > 0)
                    {
                        string name = sensor.Name.ToLower();
                        if (!name.Contains("hot") && !name.Contains("spot") && !name.Contains("memory") && !name.Contains("junction"))
                        {
                            int temp = (int)Math.Round(sensor.Value.Value);
                            if (temp > stats.GpuTemp && temp < 120)
                            {
                                stats.GpuTemp = temp;
                            }
                        }
                    }

                    if (sensor.SensorType == SensorType.Clock && sensor.Value.HasValue && sensor.Name.IndexOf("Core", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        double clockGhz = Math.Round(sensor.Value.Value / 1000.0, 2);
                        if (clockGhz > stats.GpuFreqGhz) stats.GpuFreqGhz = clockGhz;
                    }
                }
            }

            // Оперативная память
            if (hardware.HardwareType == HardwareType.Memory)
            {
                foreach (ISensor sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Data && sensor.Name == "Memory Used" && sensor.Value.HasValue)
                        stats.RamUsedGb = Math.Round(sensor.Value.Value, 1);

                    if (sensor.SensorType == SensorType.Data && sensor.Name == "Memory Available" && sensor.Value.HasValue)
                    {
                        double available = sensor.Value.Value;
                        stats.RamTotalGb = Math.Round(stats.RamUsedGb + available, 1);
                    }

                    if (sensor.SensorType == SensorType.Load && sensor.Name == "Memory" && sensor.Value.HasValue)
                    {
                        stats.RamLoadPercent = (int)Math.Round(sensor.Value.Value);
                    }
                }
            }

            foreach (IHardware subHardware in hardware.SubHardware)
            {
                ScanHardwareRecursive(subHardware, stats);
            }
        }

        public void Dispose()
        {
            try
            {
                _computer?.Close();
                _cpuEngine?.Dispose();
            }
            catch { }
        }
    }
    #endregion
}