using System.Globalization;
using GreenhouseMonitor;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using NLog;
using NLog.Config;
using NLog.Targets;

// Configuring logger.
var Log = LogManager.GetCurrentClassLogger();
var config = new LoggingConfiguration();
var logconsole = new ColoredConsoleTarget("console");
config.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);
//var logdebug = new DebuggerTarget("debugger");
//config.AddRule(LogLevel.Trace, LogLevel.Fatal, logdebug);
IotFleet.Helpers.AddFileOutputToLogger(config);
LogManager.Configuration = config;

// Load remaining environment variables.
var influxDbToken = IotFleet.Helpers.LoadEnvOrDie("INFLUXDB_TEVUKAS_TOKEN");
var bucket = IotFleet.Helpers.LoadEnvOrDie("INFLUXDB_TEVUKAS_BUCKET");
var org = IotFleet.Helpers.LoadEnvOrDie("INFLUXDB_ORG");
var influxDbHost = IotFleet.Helpers.LoadEnvOrDie("INFLUXDB_HOST", "http://127.0.0.1:8086");

// Initializing classes.
var measurements = new Measurements();
measurements.Initialize();

var display = new Display();
display.Initialize(measurements);

var homieProducer = new HomieProducer();
homieProducer.Initialize(measurements);

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
        var temperaturePoint = PointData.Measurement("GreenhouseMonitor").Field("Temperature", Convert.ToDouble(measurements.Temperature, CultureInfo.InvariantCulture)).Timestamp(DateTime.UtcNow, WritePrecision.Ns);
        var humidityPoint = PointData.Measurement("GreenhouseMonitor").Field("Humidity", Convert.ToDouble(measurements.Humidity, CultureInfo.InvariantCulture)).Timestamp(DateTime.UtcNow, WritePrecision.Ns);
        var pressurePoint = PointData.Measurement("GreenhouseMonitor").Field("Pressure", Convert.ToDouble(measurements.Pressure, CultureInfo.InvariantCulture)).Timestamp(DateTime.UtcNow, WritePrecision.Ns);
        var cpuTemperaturePoint = PointData.Measurement("GreenhouseMonitor").Field("CpuPressure", Convert.ToDouble(measurements.CpuTemperature, CultureInfo.InvariantCulture)).Timestamp(DateTime.UtcNow, WritePrecision.Ns);

        tevukasWriteApi.WritePoint(bucket, org, temperaturePoint);
        tevukasWriteApi.WritePoint(bucket, org, humidityPoint);
        tevukasWriteApi.WritePoint(bucket, org, pressurePoint);
        tevukasWriteApi.WritePoint(bucket, org, cpuTemperaturePoint);

        Thread.Sleep(5000);
    }
}).Start();

Thread.Sleep(-1);
