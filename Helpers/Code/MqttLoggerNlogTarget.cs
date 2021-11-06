using System.Text;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace TinkerforgeNodes {
    [Target("mqtt-logger")]
    public sealed class MqttLoggerNlogTarget : TargetWithLayout {
        public MqttLoggerNlogTarget() { }

        [RequiredParameter]
        public Tevux.Protocols.Mqtt.MqttClient BrokerConnection { get; set; }

        [RequiredParameter]
        public string Topic { get; set; }

        protected override void Write(LogEventInfo logEvent) {
            // Preventing recursion, so M2MQTT nuget does not log its trace message in a circular fashion.
            if (logEvent.LoggerName.StartsWith("Tevux.Protocols.Mqtt") == false) {
                var logMessage = RenderLogEvent(Layout, logEvent);

                BrokerConnection.Publish(Topic + "/" + logEvent.LoggerName + "/" + logEvent.Level, Encoding.UTF8.GetBytes(logMessage), Tevux.Protocols.Mqtt.QosLevel.AtMostOnce, false);
            }
        }
    }
}
