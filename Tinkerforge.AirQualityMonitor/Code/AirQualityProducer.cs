using System;
using System.Threading;
using System.Threading.Tasks;
using DevBot9.Protocols.Homie;
using NLog;
using Tinkerforge;

namespace AirQualityMonitor {
    class AirQualityProducer {
        private CancellationTokenSource _globalCancellationTokenSource = new CancellationTokenSource();
        private ReliableBroker _reliableBroker;

        private HostDevice _device;
        public HostFloatProperty Pressure;
        public HostFloatProperty Temperature;
        public HostFloatProperty Humidity;
        public HostFloatProperty QualityIndex;

        private DateTime _startTime = DateTime.Now;
        private HostFloatProperty _systemUptime;
        private HostStringProperty _systemIpAddress;

        public BrickletAirQuality AirQualityBricklet { get; set; }
        public static Logger Log = LogManager.GetCurrentClassLogger();

        public AirQualityProducer() { }

        public void Initialize(ReliableBroker reliableBroker) {
            Log.Info($"Initializing {nameof(AirQualityProducer)}.");

            _globalCancellationTokenSource = new CancellationTokenSource();
            _reliableBroker = reliableBroker;
            _device = DeviceFactory.CreateHostDevice("air-monitor", "Air quality monitor");
            _reliableBroker.PublishReceived += _device.HandlePublishReceived;

            Log.Info($"Creating Homie properties.");
            _device.UpdateNodeInfo("ambient", "Ambient properties", "no-type");
            Pressure = _device.CreateHostFloatProperty(PropertyType.State, "ambient", "pressure", "Pressure", 0, "hPa");
            Temperature = _device.CreateHostFloatProperty(PropertyType.State, "ambient", "temperature", "Temperature", 0, "°C");
            Humidity = _device.CreateHostFloatProperty(PropertyType.State, "ambient", "humidity", "Humidity", 0, "%");
            QualityIndex = _device.CreateHostFloatProperty(PropertyType.State, "ambient", "quality-index", "Quality index");

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
                            Pressure.Value = (float)(AirQualityBricklet.GetAirPressure() / 100.0);
                            Temperature.Value = (float)(AirQualityBricklet.GetTemperature() / 100.0);
                            Humidity.Value = (float)(AirQualityBricklet.GetHumidity() / 100.0);
                            AirQualityBricklet.GetIAQIndex(out var index, out var _);
                            QualityIndex.Value = index;
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
