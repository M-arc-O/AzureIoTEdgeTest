using System;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using Newtonsoft.Json;

using InfluxDB.Collector;
using System.Collections.Generic;
using InfluxDB.Collector.Diagnostics;
using ShipShapeShipShared;

namespace ShipShapeShipInfluxWriter
{
    class Program
    {
        static void Main(string[] args)
        {
            Init().Wait();

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s =>
                    ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        static async Task Init()
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

            Metrics.Collector = new CollectorConfiguration()
                    .Batch.AtInterval(TimeSpan.FromSeconds(2))
                    .WriteTo.InfluxDB("http://192.168.1.110:8086", "sensordata")
                            .CreateCollector();

            CollectorLog.RegisterErrorHandler((message, exception) =>
            {
                Console.WriteLine($"Infux Error. {message}: {exception}");
            });

            await ioTHubModuleClient.SetInputMessageHandlerAsync("inputFromSensors", PipeMessage, ioTHubModuleClient);
        }

        static async Task<MessageResponse> PipeMessage(Message message, object userContext)
        {
            var moduleClient = userContext as ModuleClient;
            if (moduleClient == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain " + "expected values");
            }

            try
            {
                byte[] messageBytes = message.GetBytes();
                string messageString = Encoding.UTF8.GetString(messageBytes);
                Console.WriteLine($"Received message with body: '{messageString}'");

                if (!string.IsNullOrEmpty(messageString))
                {
                    var jsonMessage = JsonConvert.DeserializeObject<SensorData>(messageString);

                    switch (jsonMessage.SensorType)
                    {
                        case SensorType.Temperature:
                            Metrics.Measure("temperature",
                                            jsonMessage.Values[0],
                                            new Dictionary<string, string> {
                                              { "area", "ship" },
                                              { "sensor", "1" } });
                            break;
                        case SensorType.Barometer:
                            Metrics.Measure("barometer",
                                            jsonMessage.Values[0],
                                            new Dictionary<string, string> {
                                              { "area", "ship" },
                                              { "sensor", "1" } });
                            break;
                        case SensorType.Gyroscope:
                            Metrics.Measure("gyroscopex",
                                            jsonMessage.Values[0],
                                            new Dictionary<string, string> {
                                              { "area", "ship" },
                                              { "sensor", "1" } });
                            Metrics.Measure("gyroscopey",
                                            jsonMessage.Values[1],
                                            new Dictionary<string, string> {
                                              { "area", "ship" },
                                              { "sensor", "1" } });
                            Metrics.Measure("gyroscopez",
                                            jsonMessage.Values[2],
                                            new Dictionary<string, string> {
                                              { "area", "ship" },
                                              { "sensor", "1" } });
                            break;
                        case SensorType.Accelerometer:
                            Metrics.Measure("accelerometerx",
                                            jsonMessage.Values[0],
                                            new Dictionary<string, string> {
                                              { "area", "ship" },
                                              { "sensor", "1" } });
                            Metrics.Measure("accelerometery",
                                            jsonMessage.Values[1],
                                            new Dictionary<string, string> {
                                              { "area", "ship" },
                                              { "sensor", "1" } });
                            Metrics.Measure("accelerometerz",
                                            jsonMessage.Values[2],
                                            new Dictionary<string, string> {
                                              { "area", "ship" },
                                              { "sensor", "1" } });
                            break;
                        case SensorType.Magnetometer:
                            Metrics.Measure("magnetometerx",
                                            jsonMessage.Values[0],
                                            new Dictionary<string, string> {
                                              { "area", "ship" },
                                              { "sensor", "1" } });
                            Metrics.Measure("magnetometery",
                                            jsonMessage.Values[1],
                                            new Dictionary<string, string> {
                                              { "area", "ship" },
                                              { "sensor", "1" } });
                            Metrics.Measure("magnetometerz",
                                            jsonMessage.Values[2],
                                            new Dictionary<string, string> {
                                              { "area", "ship" },
                                              { "sensor", "1" } });
                            break;
                    }

                    await Task.Delay(1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
            }

            return MessageResponse.Completed;
        }
    }
}
