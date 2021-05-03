using System;
using System.Threading;
using System.Threading.Tasks;
using DevBot9.Protocols.Homie;
using NLog;

namespace Tinkerforge.AirQualityMonitor {
    class AirQualityProducer {
        private CancellationTokenSource _globalCancellationTokenSource = new CancellationTokenSource();
        private ReliableBroker _reliableBroker;

        private HostDevice _device;
        private HostFloatProperty _pressure;
        private HostFloatProperty _temperature;
        private HostFloatProperty _humidity;
        private HostFloatProperty _qualityIndex;

        private DateTime _startTime = DateTime.Now;
        private HostFloatProperty _systemUptime;
        private HostStringProperty _systemIpAddress;

        public BrickletAirQuality AirQualityBricklet { get; set; }
        public static Logger Log = LogManager.GetLogger("RackMonitor.RackMonitorProducer");

        public AirQualityProducer() { }

        public void Initialize(ReliableBroker reliableBroker) {
            Log.Info($"Initializing {nameof(AirQualityProducer)}.");

            _globalCancellationTokenSource = new CancellationTokenSource();
            _reliableBroker = reliableBroker;
            _device = DeviceFactory.CreateHostDevice("air-monitor", "Air quality monitor");
            _reliableBroker.PublishReceived += _device.HandlePublishReceived;

            Log.Info($"Creating Homie properties.");
            _device.UpdateNodeInfo("ambient", "Ambient properties", "no-type");
            _pressure = _device.CreateHostFloatProperty(PropertyType.State, "ambient", "pressure", "Pressure", 0, "hPa");
            _temperature = _device.CreateHostFloatProperty(PropertyType.State, "ambient", "temperature", "Temperature", 0, "°C");
            _humidity = _device.CreateHostFloatProperty(PropertyType.State, "ambient", "humidity", "Humidity", 0, "%");
            _qualityIndex = _device.CreateHostFloatProperty(PropertyType.State, "ambient", "quality-index", "Quality index");

            _device.UpdateNodeInfo("system", "System", "no-type");
            _systemUptime = _device.CreateHostFloatProperty(PropertyType.State, "system", "uptime", "Uptime", 0, "h");
            _systemIpAddress = _device.CreateHostStringProperty(PropertyType.State, "system", "ip-address", "IP address", Program.GetLocalIpAddress());

            Log.Info($"Initializing Homie entities.");
            _device.Initialize(_reliableBroker.PublishToTopic, _reliableBroker.SubscribeToTopic);

            Task.Run(async () => {
                Log.Info($"Spinning up parameter monitoring task.");
                while (_globalCancellationTokenSource.IsCancellationRequested == false) {
                    try {
                        if (AirQualityBricklet != null) {
                            _pressure.Value = (float)(AirQualityBricklet.GetAirPressure() / 100.0);
                            _temperature.Value = (float)(AirQualityBricklet.GetTemperature() / 100.0);
                            _humidity.Value = (float)(AirQualityBricklet.GetHumidity() / 100.0);
                            AirQualityBricklet.GetIAQIndex(out var index, out var _);
                            _qualityIndex.Value = index;
                        }
                    }
                    catch (Exception) {
                        // Sometimes this happens. No problem, swallowing, and giving some time to recover.
                        Log.Info("Reading Tinkerforge humidity bricklet timeouted.");
                        await Task.Delay(2000);
                    }

                    _systemUptime.Value = (float)(DateTime.Now - _startTime).TotalHours;
                    _systemIpAddress.Value = Program.GetLocalIpAddress();

                    await Task.Delay(5000);
                }
            });
        }
    }
}
