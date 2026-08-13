using OpenHardwareMonitor.Hardware;

namespace MyDynamicIsland.Modules
{
    /// <summary>
    /// Структура для передачи полных метрик процессора
    /// </summary>
    public struct CpuData
    {
        public int LoadPercent { get; set; }
        public double AverageFreqGhz { get; set; }
        public int CoreMaxTemp { get; set; }
        public double PackagePowerWatts { get; set; }
    }

    /// <summary>
    /// Класс-визитор для обновления состояния сенсоров OpenHardwareMonitor
    /// </summary>
    public class CpuUpdateVisitor : IVisitor
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

    /// <summary>
    /// Автономный изолированный движок мониторинга процессора на базе OpenHardwareMonitor
    /// </summary>
    public class CpuMonitorEngine : IDisposable
    {
        private readonly Computer _computer;
        private readonly CpuUpdateVisitor _visitor = new CpuUpdateVisitor();

        public CpuMonitorEngine()
        {
            // Инициализируем опрос исключительно для процессора
            _computer = new Computer { IsCpuEnabled = true };

            try
            {
                // КРИТИЧЕСКИ ВАЖНО: передаем true для принудительного запуска драйвера ядра WinRing0
                _computer.Open(true);
            }
            catch
            {
                // Ошибка инициализации драйвера (например, запуск без прав Администратора)
            }
        }

        /// <summary>
        /// Возвращает актуальный набор данных процессора:
        /// - Загрузка процессора (%)
        /// - Средняя частота всех ядер (ГГц)
        /// - Максимальная температура ядер Core Max (°C)
        /// - Мощность CPU Package (Вт)
        /// </summary>
        public CpuData GetCpuData()
        {
            var data = new CpuData();
            var coreClocksMhz = new List<float>();

            try
            {
                // Принудительно опрашиваем все сенсоры процессора
                _computer.Accept(_visitor);

                foreach (IHardware hardware in _computer.Hardware)
                {
                    if (hardware.HardwareType == HardwareType.Cpu)
                    {
                        foreach (ISensor sensor in hardware.Sensors)
                        {
                            if (!sensor.Value.HasValue) continue;

                            // 1. Загрузка процессора ("Total")
                            if (sensor.SensorType == SensorType.Load)
                            {
                                if (sensor.Name.EndsWith("Total", StringComparison.OrdinalIgnoreCase))
                                {
                                    data.LoadPercent = (int)Math.Round(sensor.Value.Value);
                                }
                            }

                            // 2. Температура ("Core Max")
                            if (sensor.SensorType == SensorType.Temperature)
                            {
                                if (sensor.Name.Equals("Core Max", StringComparison.OrdinalIgnoreCase))
                                {
                                    data.CoreMaxTemp = (int)Math.Round(sensor.Value.Value);
                                }
                            }

                            // 3. Мощность ("CPU Package")
                            if (sensor.SensorType == SensorType.Power)
                            {
                                if (sensor.Name.Equals("CPU Package", StringComparison.OrdinalIgnoreCase))
                                {
                                    data.PackagePowerWatts = Math.Round(sensor.Value.Value, 1);
                                }
                            }

                            // 4. Частота всех отдельных ядер (P-Core, E-Core, CPU Core)
                            if (sensor.SensorType == SensorType.Clock)
                            {
                                if (sensor.Name.Contains("Core") && !sensor.Name.Contains("Bus"))
                                {
                                    coreClocksMhz.Add(sensor.Value.Value);
                                }
                            }
                        }
                    }
                }

                // Вычисляем среднюю частоту по всем найденным ядрам и переводим из МГц в ГГц
                if (coreClocksMhz.Any())
                {
                    double avgMhz = coreClocksMhz.Average();
                    data.AverageFreqGhz = Math.Round(avgMhz / 1000.0, 2);
                }
            }
            catch
            {
                // При возникновении исключения вернем пустую структуру data
            }

            return data;
        }

        public void Dispose()
        {
            try
            {
                _computer?.Close();
            }
            catch { }
        }
    }
}