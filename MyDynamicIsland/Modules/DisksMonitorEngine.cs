using System.IO;
using LibreHardwareMonitor.Hardware;

namespace MyDynamicIsland.Modules
{
    
    /// Модель данных информации о диске
    
    public class DiskRawData
    {
        public string? DriveLetter { get; set; }
        public long UsedGb { get; set; }
        public long TotalGb { get; set; }
        public int UsedPercent { get; set; }
        public int PowerOnHours { get; set; }
    }

    
    /// Вспомогательный визитер для обновления сенсоров дисков в LibreHardwareMonitor
    
    public class StorageUpdateVisitor : IVisitor
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

    
    /// Чистый движок мониторинга накопителей на базе LibreHardwareMonitorLib.
    /// Точно считывает датчик 'Power On Hours' типа SensorType.Factor без искажений.
    
    public class DisksMonitorEngine : IDisposable
    {
        private readonly Computer _computer;
        private readonly StorageUpdateVisitor _updateVisitor;

        public DisksMonitorEngine()
        {
            _updateVisitor = new StorageUpdateVisitor();

            // Включаем опрос устройств хранения данных
            _computer = new Computer
            {
                IsStorageEnabled = true,
                IsCpuEnabled = false,
                IsGpuEnabled = false,
                IsMemoryEnabled = false,
                IsMotherboardEnabled = false,
                IsControllerEnabled = false,
                IsNetworkEnabled = false
            };

            try
            {
                _computer.Open();
            }
            catch { }
        }

        
        /// Возвращает актуальные данные обо всех дисках системы
        
        public List<DiskRawData> GetDisksData()
        {
            var result = new List<DiskRawData>();

            try
            {
                // 1. Обновляем датчики в LibreHardwareMonitor
                _computer.Accept(_updateVisitor);

                // 2. Считываем карту точных часов S.M.A.R.T. для каждого физического диска
                var storageHoursMap = GetStorageHoursFromLibreHardware();

                // 3. Обходим логические диски системы (C:, D:, W: и т.д.)
                var drives = DriveInfo.GetDrives();
                int driveIndex = 0;

                foreach (var drive in drives)
                {
                    if (!drive.IsReady || drive.DriveType != DriveType.Fixed) continue;

                    string driveLetter = drive.Name.TrimEnd('\\'); // "C:", "D:", "W:"
                    long totalGb = drive.TotalSize / (1024 * 1024 * 1024);
                    long freeGb = drive.TotalFreeSpace / (1024 * 1024 * 1024);
                    long usedGb = totalGb - freeGb;
                    int usedPercent = totalGb > 0 ? (int)((usedGb * 100) / totalGb) : 0;

                    // Выбираем индивидуальные часы для физического накопителя по его индексу
                    int hours = 0;
                    if (storageHoursMap.Count > driveIndex)
                    {
                        hours = storageHoursMap.Values.ElementAt(driveIndex);
                    }
                    else if (storageHoursMap.Count > 0)
                    {
                        hours = storageHoursMap.Values.FirstOrDefault();
                    }

                    result.Add(new DiskRawData
                    {
                        DriveLetter = driveLetter,
                        UsedGb = usedGb,
                        TotalGb = totalGb,
                        UsedPercent = usedPercent,
                        PowerOnHours = hours
                    });

                    driveIndex++;
                }
            }
            catch { }

            return result;
        }

        
        /// Извлекает точные часы работы из сенсора 'Power On Hours' у LibreHardwareMonitorLib
        
        private Dictionary<string, int> GetStorageHoursFromLibreHardware()
        {
            var hoursMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            try
            {
                foreach (IHardware hardware in _computer.Hardware)
                {
                    if (hardware.HardwareType == HardwareType.Storage)
                    {
                        int detectedHours = 0;

                        foreach (ISensor sensor in hardware.Sensors)
                        {
                            if (!sensor.Value.HasValue) continue;

                            string sensorName = sensor.Name.Trim();

                            // Точно ищем 'Power On Hours' и исключаем 'Power On Count'
                            if (sensorName.Equals("Power On Hours", StringComparison.OrdinalIgnoreCase))
                            {
                                detectedHours = (int)Math.Round(sensor.Value.Value);
                                if (detectedHours > 0) break;
                            }
                        }

                        // Если не нашли по точному имени, ищем по совпадению ключевых слов (запасной вариант)
                        if (detectedHours == 0)
                        {
                            foreach (ISensor sensor in hardware.Sensors)
                            {
                                if (!sensor.Value.HasValue) continue;

                                string sensorName = sensor.Name.ToLower();

                                if (sensorName.Contains("power on hours") || sensorName.Contains("power-on hours"))
                                {
                                    detectedHours = (int)Math.Round(sensor.Value.Value);
                                    if (detectedHours > 0) break;
                                }
                            }
                        }

                        hoursMap[hardware.Name] = detectedHours;
                    }
                }
            }
            catch { }

            return hoursMap;
        }

        public void Dispose()
        {
            try
            {
                _computer.Close();
            }
            catch { }
        }
    }
}