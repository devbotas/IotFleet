using System;
using System.Threading;
using System.Threading.Tasks;
using DevBot9.Protocols.Homie;
using DevBot9.Protocols.Homie.Utilities;
using NLog;
using Tevux.Protocols.Mqtt;
using Tinkerforge;

namespace AirQualityMonitor {
    class AirQualityProducer {
        private CancellationTokenSource _globalCancellationTokenSource = new();
        private readonly YahiTevuxHostConnection _broker = new();
        private HostDevice _device;
        private readonly byte[] _digits = { 0x3f, 0x06, 0x5b, 0x4f, 0x66, 0x6d, 0x7d, 0x07, 0x7f, 0x6f, 0x77, 0x7c, 0x39, 0x5e, 0x79, 0x71 }; // 0~9,A,B,C,D,E,F
        private static bool _showColon;

        public HostNumberProperty Pressure;
        public HostNumberProperty Temperature;
        public HostNumberProperty Humidity;
        public HostNumberProperty QualityIndex;

        private readonly DateTime _startTime = DateTime.Now;
        private HostNumberProperty _systemUptime;
        private HostTextProperty _systemStatus;

        public BrickletAirQuality AirQualityBricklet { get; set; }
        public BrickletSegmentDisplay4x7 SegmentDisplayBricklet { get; set; }
        public static Logger Log = LogManager.GetCurrentClassLogger();

        public AirQualityProducer() { }

        public void Initialize(ChannelConnectionOptions connectionOptions) {
            Log.Info($"Initializing {nameof(AirQualityProducer)}.");

            _globalCancellationTokenSource = new CancellationTokenSource();
            _device = DeviceFactory.CreateHostDevice("air-monitor", "Air quality monitor");

            Log.Info($"Creating Homie properties.");
            _device.UpdateNodeInfo("ambient", "Ambient properties", "no-type");
            Pressure = _device.CreateHostNumberProperty(PropertyType.State, "ambient", "pressure", "Pressure", 0, "hPa");
            Temperature = _device.CreateHostNumberProperty(PropertyType.State, "ambient", "temperature", "Temperature", 0, "°C");
            Humidity = _device.CreateHostNumberProperty(PropertyType.State, "ambient", "humidity", "Humidity", 0, "%");
            QualityIndex = _device.CreateHostNumberProperty(PropertyType.State, "ambient", "quality-index", "Quality index");

            _device.UpdateNodeInfo("system", "System", "no-type");
            _systemUptime = _device.CreateHostNumberProperty(PropertyType.State, "system", "uptime", "Uptime", 0, "h");
            _systemStatus = _device.CreateHostTextProperty(PropertyType.State, "system", "status", "Status", "Healthy");

            Log.Info($"Initializing Homie entities.");
            _broker.Initialize(connectionOptions);
            _device.Initialize(_broker);

            Task.Run(async () => {
                var timeoutCounter = 0;

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

                        if (SegmentDisplayBricklet != null) {
                            var temperatureLowerDigit = (int)(Math.Round(Temperature.Value, 0) % 10);
                            var temperatureHigherDigit = (int)((Math.Round(Temperature.Value, 0) - temperatureLowerDigit) / 10);

                            var humidityLowerDigit = (int)(Math.Round(Humidity.Value, 0) % 10);
                            var humidityHigherDigit = (int)((Math.Round(Humidity.Value, 0) - humidityLowerDigit) / 10);

                            SegmentDisplayBricklet.SetSegments(new[] { _digits[temperatureHigherDigit], _digits[temperatureLowerDigit], _digits[humidityHigherDigit], _digits[humidityLowerDigit] }, 0, _showColon);
                            _showColon = !_showColon;
                        }

                        if (timeoutCounter > 0) {
                            _systemStatus.Value = "Healthy";
                            _device.SetState(HomieState.Ready);
                            timeoutCounter = 0;
                        }
                    }
                    catch (Exception) {
                        // Sometimes this happens. No problem, swallowing, and giving some time to recover.
                        Log.Info("Reading Tinkerforge humidity bricklet timeouted.");
                        timeoutCounter++;
                        await Task.Delay(2000);
                    }

                    _systemUptime.Value = (float)(DateTime.Now - _startTime).TotalHours;

                    if (timeoutCounter > 3) {
                        _systemStatus.Value = "Bricklet is missing!";
                        _device.SetState(HomieState.Alert);
                    }

                    await Task.Delay(5000);
                }
            });
        }

        public void HandleEnumeration(string UID, int deviceIdentifier, IPConnection brickConnection, short enumerationType) {
            if (enumerationType == IPConnection.ENUMERATION_TYPE_CONNECTED || enumerationType == IPConnection.ENUMERATION_TYPE_AVAILABLE) {
                if (deviceIdentifier == BrickletAirQuality.DEVICE_IDENTIFIER) {
                    var airQualityBricklet = new BrickletAirQuality(UID, brickConnection);

                    Log.Info($"Found Air quality bricklet {UID}.");
                    AirQualityBricklet = airQualityBricklet;
                }

                if (deviceIdentifier == BrickletSegmentDisplay4x7.DEVICE_IDENTIFIER) {
                    var segmentDisplayBricklet = new BrickletSegmentDisplay4x7(UID, brickConnection);

                    Log.Info($"Found segment display {UID}.");
                    SegmentDisplayBricklet = segmentDisplayBricklet;
                }
            }
        }
    }
}
