﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevBot9.Protocols.Homie;
using DevBot9.Protocols.Homie.Utilities;
using NLog;

namespace GreenhouseMonitor;
public class HomieProducer {
    private CancellationTokenSource _globalCancellationTokenSource = new CancellationTokenSource();
    private YahiTevuxHostConnection _broker = new YahiTevuxHostConnection();
    private HostDevice _device;
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    public HostNumberProperty Pressure;
    public HostNumberProperty Temperature;
    public HostNumberProperty Humidity;

    private DateTime _startTime = DateTime.Now;
    private HostNumberProperty _systemUptime;
    private HostTextProperty _systemStatus;

    public HomieProducer() { }

    public void Initialize(Measurements measurements) {
        var brokerIp = IotFleet.Helpers.LoadEnvOrDie("MQTT_BROKER_IP", "127.0.0.1");
        var channelOptions = new Tevux.Protocols.Mqtt.ChannelConnectionOptions();
        channelOptions.SetHostname(brokerIp);

        DeviceFactory.Initialize("homie");

        _log.Info($"Initializing {nameof(HomieProducer)}.");

        _globalCancellationTokenSource = new CancellationTokenSource();
        _device = DeviceFactory.CreateHostDevice("greenhouse-monitor", "Greenhouse monitor");

        _log.Info($"Creating Homie properties.");
        _device.UpdateNodeInfo("ambient", "Ambient properties", "no-type");
        Pressure = _device.CreateHostNumberProperty(PropertyType.State, "ambient", "pressure", "Pressure", 0, "hPa");
        Temperature = _device.CreateHostNumberProperty(PropertyType.State, "ambient", "temperature", "Temperature", 0, "°C");
        Humidity = _device.CreateHostNumberProperty(PropertyType.State, "ambient", "humidity", "Humidity", 0, "%");

        _device.UpdateNodeInfo("system", "System", "no-type");
        _systemUptime = _device.CreateHostNumberProperty(PropertyType.State, "system", "uptime", "Uptime", 0, "h");
        _systemStatus = _device.CreateHostTextProperty(PropertyType.State, "system", "status", "Status", "Healthy");

        _log.Info($"Initializing Homie entities.");
        _broker.Initialize(channelOptions);
        _device.Initialize(_broker);

        Task.Run(async () => {
            var timeoutCounter = 0;

            _log.Info($"Spinning up parameter monitoring task.");
            while (_globalCancellationTokenSource.IsCancellationRequested == false) {

                Temperature.Value = measurements.Temperature;
                Humidity.Value = measurements.Humidity;
                Pressure.Value = measurements.Pressure;

                _systemUptime.Value = (float)(DateTime.Now - _startTime).TotalHours;

                if (timeoutCounter > 3) {
                    _systemStatus.Value = "Something went wrong";
                    _device.SetState(HomieState.Alert);
                }

                await Task.Delay(1000);
            }
        });
    }
}

