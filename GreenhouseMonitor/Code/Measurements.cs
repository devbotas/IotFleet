using System.Device.I2c;
using System.Runtime.InteropServices;
using Iot.Device.Bmxx80;

namespace GreenhouseMonitor;

public class Measurements {
    public double Temperature { get; private set; }
    public double Humidity { get; private set; }
    public double Pressure { get; private set; }

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

        while (true) {
            var readResult = bme280.Read();

            Temperature = readResult.Temperature?.DegreesCelsius ?? 0;
            Pressure = readResult.Pressure?.Hectopascals ?? 0;
            Humidity = readResult.Humidity?.Percent ?? 0;

            Thread.Sleep(1000);
        }
    }
}
