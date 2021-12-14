using GreenhouseMonitor;

var measurements = new Measurements();
measurements.Initialize();

var display = new Display();
display.Initialize(measurements);

var homieProducer = new HomieProducer();
homieProducer.Initialize(measurements);

Thread.Sleep(-1);
