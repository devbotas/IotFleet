using System;
using System.Globalization;
using System.Threading;
using DevBot9.Protocols.Homie;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using NLog;
using NLog.Config;
using NLog.Targets;
using Tinkerforge;

namespace ShedMonitor;
class Program {
    private static IPConnection _brickConnection;
    private static readonly ShedMonitorProducer _shedMonitorProducer = new();
    public static Logger Log = LogManager.GetCurrentClassLogger();

    static void Main(string[] args) {
        Target.Register<IotFleet.MqttLoggerNlogTarget>("mqtt-logger");

        var brokerIp = IotFleet.Helpers.LoadEnvOrDie("MQTT_BROKER_IP", "127.0.0.1");
        var channelOptions = new Tevux.Protocols.Mqtt.ChannelConnectionOptions();
        channelOptions.SetHostname(brokerIp);

        var config = new LoggingConfiguration();
        var logConnection = new Tevux.Protocols.Mqtt.MqttClient();
        logConnection.Initialize();
        logConnection.ConnectAndWait(channelOptions);

        var logconsole = new ColoredConsoleTarget("console");
        config.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);

        var logdebug = new DebuggerTarget("debugger");
        config.AddRule(LogLevel.Trace, LogLevel.Fatal, logdebug);

        IotFleet.Helpers.AddFileOutputToLogger(config);
        IotFleet.Helpers.AddMqttOutputToLogger(config, logConnection);

        LogManager.Configuration = config;

        // Load remaining environment variables.
        var shedMonitorIp = IotFleet.Helpers.LoadEnvOrDie("SHED_MONITOR_IP", "127.0.0.1");
        var influxDbToken = IotFleet.Helpers.LoadEnvOrDie("INFLUXDB_TEVUKAS_TOKEN");
        var bucket = IotFleet.Helpers.LoadEnvOrDie("INFLUXDB_TEVUKAS_BUCKET");
        var org = IotFleet.Helpers.LoadEnvOrDie("INFLUXDB_ORG");
        var influxDbHost = IotFleet.Helpers.LoadEnvOrDie("INFLUXDB_HOST", "http://127.0.0.1:8086");

        // Initializing classes.
        Log.Info("Initializing connections.");
        DeviceFactory.Initialize("homie");
        _shedMonitorProducer.Initialize(channelOptions);

        // Connecting to bricklets.
        _brickConnection = new IPConnection();
        _brickConnection.EnumerateCallback += HandleEnumeration;
        _brickConnection.Connected += HandleConnection;
        _brickConnection.Connect(shedMonitorIp, 4223);

        // InfluxDB part.
        Log.Info("Initializing InfluxDB.");
        var tevukasSystemClient = InfluxDBClientFactory.Create(influxDbHost, influxDbToken.ToCharArray());
        var tevukasWriteApi = tevukasSystemClient.GetWriteApi();
        tevukasWriteApi.EventHandler += (sender, e) => {
            if (e is WriteErrorEvent) {
                Log.Warn("Cannot write to InfluxDB. Unfortunately, InfluxDB does not provide any useful debug information :(");
            }
        };

        new Thread(() => {
            while (true) {
                var temperaturePoint = PointData.Measurement("ShedMonitor").Field("Temperature", Convert.ToDouble(_shedMonitorProducer.Temperature.Value, CultureInfo.InvariantCulture)).Timestamp(DateTime.UtcNow, WritePrecision.Ns);
                var humidityPoint = PointData.Measurement("ShedMonitor").Field("Humidity", Convert.ToDouble(_shedMonitorProducer.Humidity.Value, CultureInfo.InvariantCulture)).Timestamp(DateTime.UtcNow, WritePrecision.Ns);
                var pressurePoint = PointData.Measurement("ShedMonitor").Field("Pressure", Convert.ToDouble(_shedMonitorProducer.Pressure.Value, CultureInfo.InvariantCulture)).Timestamp(DateTime.UtcNow, WritePrecision.Ns);
                var qualityIndexPoint = PointData.Measurement("ShedMonitor").Field("QualityIndex", Convert.ToDouble(_shedMonitorProducer.QualityIndex.Value, CultureInfo.InvariantCulture)).Timestamp(DateTime.UtcNow, WritePrecision.Ns);
                var waterPressurePoint = PointData.Measurement("ShedMonitor").Field("WaterPressure", Convert.ToDouble(_shedMonitorProducer.WaterPressure.Value, CultureInfo.InvariantCulture)).Timestamp(DateTime.UtcNow, WritePrecision.Ns);

                tevukasWriteApi.WritePoint(bucket, org, temperaturePoint);
                tevukasWriteApi.WritePoint(bucket, org, humidityPoint);
                tevukasWriteApi.WritePoint(bucket, org, pressurePoint);
                tevukasWriteApi.WritePoint(bucket, org, qualityIndexPoint);
                tevukasWriteApi.WritePoint(bucket, org, waterPressurePoint);

                Thread.Sleep(5000);
            }
        }).Start();

        Log.Info("Application started.");
    }

    static void HandleConnection(IPConnection sender, short connectReason) {
        Log.Info("Connection to BrickDaemon has been established. Doing the (re)initialization.");

        // Enumerate devices again. If we reconnected, the Bricks/Bricklets may have been offline and the configuration may be lost.
        // In this case we don't care for the reason of the connection.
        Log.Info("Beginning re-enumeration of bricklets.");
        _brickConnection.Enumerate();
    }

    public static void HandleEnumeration(IPConnection sender, string UID, string connectedUID, char position, short[] hardwareVersion, short[] firmwareVersion, int deviceIdentifier, short enumerationType) {
        _shedMonitorProducer.HandleEnumeration(UID, deviceIdentifier, _brickConnection, enumerationType);
    }
}
