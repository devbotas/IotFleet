﻿using System;
using System.Threading;
using System.Threading.Tasks;
using DevBot9.Protocols.Homie;
using DevBot9.Protocols.Homie.Utilities;
using NLog;
using Tinkerforge;

namespace AirQualityMonitor {
    class AirQualityProducer {
        private CancellationTokenSource _globalCancellationTokenSource = new CancellationTokenSource();
        private IMqttBroker _broker = new PahoBroker();

        private HostDevice _device;
        public HostNumberProperty Pressure;
        public HostNumberProperty Temperature;
        public HostNumberProperty Humidity;
        public HostNumberProperty QualityIndex;

        private DateTime _startTime = DateTime.Now;
        private HostNumberProperty _systemUptime;
        private HostTextProperty _systemIpAddress;

        public BrickletAirQuality AirQualityBricklet { get; set; }
        public static Logger Log = LogManager.GetCurrentClassLogger();

        public AirQualityProducer() { }

        public void Initialize(string mqttBrokerIpAddress) {
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
            _systemIpAddress = _device.CreateHostTextProperty(PropertyType.State, "system", "ip-address", "IP address", Program.GetLocalIpAddress());

            Log.Info($"Initializing Homie entities.");
            _broker.Initialize(mqttBrokerIpAddress);
            _device.Initialize(_broker, (severity, message) => {
                if (severity == "Info") { Log.Info(message); }
                else if (severity == "Error") { Log.Error(message); }
                else { Log.Debug(message); }
            });

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
