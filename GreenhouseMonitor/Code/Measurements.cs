using System.Device.I2c;
using System.Runtime.InteropServices;
using Iot.Device.Bmxx80;
using Iot.Device.CpuTemperature;

namespace GreenhouseMonitor;

public class Measurements {
    public double Temperature { get; private set; }
    public double Humidity { get; private set; }
    public double Pressure { get; private set; }
    public double CpuTemperature { get; private set; }

    public Measurements() {


    }

    public void Initialize() {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            new Thread(() => { RunFakeMeasurementLoop(); }).Start();
        }
        else {
            new Thread(() => { RunRealMeasurementLoop(); }).Start();
        }
    }

    private void RunFakeMeasurementLoop() {
        var randomNumber = new Random(DateTime.Now.Millisecond);
        while (true) {
            Temperature = 20 + randomNumber.NextDouble() * 3;
            Humidity = 50 + randomNumber.NextDouble() * 3;
            Pressure = 999 + randomNumber.NextDouble() * 20;
            CpuTemperature = 40 + randomNumber.NextDouble() * 10;
            Thread.Sleep(1000);
        }
    }

    private void RunRealMeasurementLoop() {
        I2cConnectionSettings bme280connectionSettings = new(1, 0x76);
        using var bme280Device = I2cDevice.Create(bme280connectionSettings);
        using var bme280 = new Bme280(bme280Device) {
            TemperatureSampling = Sampling.LowPower,
            PressureSampling = Sampling.UltraHighResolution,
            HumiditySampling = Sampling.Standard,
        };

        using var cpuTemperature = new CpuTemperature();

        while (true) {
            var readResult = bme280.Read();

            var cpuTemperatures = new List<double>();
            var systemTemperatures = cpuTemperature.ReadTemperatures();

            // There may be multiple system sensors. I'll just pick first one that has a valid value.
            if (systemTemperatures.Count > 0) {
                if (double.IsNaN(systemTemperatures[0].Temperature.DegreesCelsius) == false) {
                    CpuTemperature = systemTemperatures[0].Temperature.DegreesCelsius;
                    cpuTemperatures.Add(CpuTemperature);
                    if (cpuTemperatures.Count > 10) { cpuTemperatures.RemoveAt(0); }
                }
            }
            CpuTemperature = cpuTemperatures.Average();

            Temperature = readResult.Temperature?.DegreesCelsius ?? -99;
            Pressure = readResult.Pressure?.Hectopascals ?? -99;
            Humidity = readResult.Humidity?.Percent ?? -99;

            Thread.Sleep(1000);
        }
    }
}
