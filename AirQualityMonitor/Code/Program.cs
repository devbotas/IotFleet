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
using TinkerforgeNodes;

namespace AirQualityMonitor {
    class Program {
        private static IPConnection _brickConnection;
        private static string _localHostname = "no-hostname";
        private static string _localIpAddress = "0.0.0.0";
        private static AirQualityProducer _airQualityProducer = new AirQualityProducer();
        public static Logger Log = LogManager.GetCurrentClassLogger();

        static void Main() {
            Target.Register<MqttLoggerNlogTarget>("mqtt-logger");

            var brokerIp = TinkerforgeNodes.Helpers.LoadEnvOrDie("MQTT_BROKER_IP", "127.0.0.1");
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

            TinkerforgeNodes.Helpers.AddFileOutputToLogger(config);
            TinkerforgeNodes.Helpers.AddMqttOutputToLogger(config, logConnection);

            LogManager.Configuration = config;

            // Load remaining environment variables.
            var airQualityIp = TinkerforgeNodes.Helpers.LoadEnvOrDie("AIR_QUALITY_IP", "127.0.0.1");
            var airQualityToken = TinkerforgeNodes.Helpers.LoadEnvOrDie("INFLUXDB_TEVUKAS_TOKEN");
            var bucket = TinkerforgeNodes.Helpers.LoadEnvOrDie("INFLUXDB_TEVUKAS_BUCKET");
            var org = TinkerforgeNodes.Helpers.LoadEnvOrDie("INFLUXDB_ORG");

            // Initializing classes.
            Log.Info("Initializing connections.");
            DeviceFactory.Initialize("homie");
            _airQualityProducer.Initialize(channelOptions);

            // Connecting to bricklets.
            _brickConnection = new IPConnection();
            _brickConnection.EnumerateCallback += HandleEnumeration;
            _brickConnection.Connected += HandleConnection;
            _brickConnection.Connect(airQualityIp, 4223);

            // InfluxDB part.
            Log.Info("Initializing InfluxDB.");
            var tevukasSystemClient = InfluxDBClientFactory.Create("https://westeurope-1.azure.cloud2.influxdata.com", airQualityToken.ToCharArray());
            new Thread(() => {
                while (true) {
                    var temperaturePoint = PointData.Measurement("AirQuality").Field("Temperature", Convert.ToDouble(_airQualityProducer.Temperature.Value, CultureInfo.InvariantCulture)).Timestamp(DateTime.UtcNow, WritePrecision.Ns);
                    var humidityPoint = PointData.Measurement("AirQuality").Field("Humidity", Convert.ToDouble(_airQualityProducer.Humidity.Value, CultureInfo.InvariantCulture)).Timestamp(DateTime.UtcNow, WritePrecision.Ns);
                    var pressurePoint = PointData.Measurement("AirQuality").Field("Pressure", Convert.ToDouble(_airQualityProducer.Pressure.Value, CultureInfo.InvariantCulture)).Timestamp(DateTime.UtcNow, WritePrecision.Ns);
                    var qualityIndexPoint = PointData.Measurement("AirQuality").Field("QualityIndex", Convert.ToDouble(_airQualityProducer.QualityIndex.Value, CultureInfo.InvariantCulture)).Timestamp(DateTime.UtcNow, WritePrecision.Ns);

                    using (var tevukasWriteApi = tevukasSystemClient.GetWriteApi()) {
                        tevukasWriteApi.WritePoint("TevukasSystem", org, temperaturePoint);
                        tevukasWriteApi.WritePoint("TevukasSystem", org, humidityPoint);
                        tevukasWriteApi.WritePoint("TevukasSystem", org, pressurePoint);
                        tevukasWriteApi.WritePoint("TevukasSystem", org, qualityIndexPoint);
                    }
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
            _airQualityProducer.HandleEnumeration(UID, deviceIdentifier, _brickConnection, enumerationType);
        }
    }
}
