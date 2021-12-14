using System.Device.Spi;
using System.Runtime.InteropServices;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Tevux.Device.St7735;

namespace GreenhouseMonitor;
public class Display {
    public void Initialize(Measurements measurements) {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            // Initializing busses.
            var spiSettings = new SpiConnectionSettings(0, 1);
            spiSettings.ClockFrequency = 12_000_000;
            var spiBus = SpiDevice.Create(spiSettings);
            var gpioController = new System.Device.Gpio.GpioController();

            // Initializing display.
            var lcd = new ST7735();
            lcd.InitializeAsPimoroniEnviro(spiBus, gpioController);
            lcd.SetOrientation(Orientation.Rotated270);
            lcd.Clear();

            // Now goes fonts and colors.
            var smallFont = SystemFonts.CreateFont("DejaVu Sans", 16, FontStyle.Regular);
            var bigFont = SystemFonts.CreateFont("DejaVu Sans", 32, FontStyle.Regular);
            var emojiFont = SystemFonts.CreateFont("Symbola", 29, FontStyle.Regular);

            var backgroundColor = Color.Purple;
            var foregroundColor = Color.White;

            // Drawing emoji symbols (these will need refreshing).
            var thermometerImage = new Image<Bgr565>(29, 39);
            thermometerImage.Mutate(ctx => ctx.Fill(backgroundColor).DrawText("🌡", emojiFont, foregroundColor, new PointF(0, 2)));
            lcd.SetRegion(0, 0, 29, 39);
            lcd.SendBitmap(thermometerImage.ToBgr565Array());

            var dropletImage = new Image<Bgr565>(29, 39);
            dropletImage.Mutate(ctx => ctx.Fill(backgroundColor).DrawText("💧", emojiFont, foregroundColor, new PointF(8, 2)));
            lcd.SetRegion(0, 40, 29, 39);
            lcd.SendBitmap(dropletImage.ToBgr565Array());

            // Starting the vindaloop!
            new Thread(() => {
                var image = new Image<Bgr565>(80, 39);
                while (true) {
                    // Drawing temperature.
                    image.Mutate(ctx => ctx.Fill(Color.Purple));
                    image.Mutate(ctx => ctx.Fill(backgroundColor).DrawText(measurements.Temperature.ToString("F1"), bigFont, foregroundColor, new PointF(0, 2)));
                    lcd.SetRegion(29, 0, 80, 39);
                    lcd.SendBitmap(image.ToBgr565Array());

                    // Drawing humidity.
                    image.Mutate(ctx => ctx.Fill(backgroundColor).DrawText(measurements.Humidity.ToString("F0") + "%", bigFont, foregroundColor, new PointF(0, 2)));
                    lcd.SetRegion(29, 40, 80, 39);
                    lcd.SendBitmap(image.ToBgr565Array());

                    // Drawing pressure (and maybe other less important stuff, too).
                    var secondaryPanel = new Image<Bgr565>(80, 47);
                    secondaryPanel.Mutate(ctx => ctx.Fill(backgroundColor).DrawText(measurements.Pressure.ToString("F0") + "hPa", smallFont, foregroundColor, new PointF(4, 0)));
                    secondaryPanel.Mutate(ctx => ctx.Rotate(270));
                    lcd.SetRegion(110, 0, 47, 80);
                    lcd.SendBitmap(secondaryPanel.ToBgr565Array());

                    Thread.Sleep(500);
                }

            }).Start();
        }
    }

}

public static class SixLaborsExtensions {
    /// <summary>
    /// Extension method to transcode SixLabors image representation to byte array that is supported by ST7735.
    /// </summary>
    public static byte[] ToBgr565Array(this Image<Bgr565> image) {
        var returnArray = new byte[image.Width * image.Height * 2];
        for (var j = 0; j < image.Height; j++) {
            for (var i = 0; i < image.Width; i++) {
                returnArray[2 * (i + image.Width * j)] = (byte)(image[i, j].PackedValue >> 8);
                returnArray[2 * (i + image.Width * j) + 1] = (byte)image[i, j].PackedValue;
            }
        }
        return returnArray;
    }
}
