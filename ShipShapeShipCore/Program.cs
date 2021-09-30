using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using ShipShapeShipShared;
using ShipShapeShipShared.Messages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShipShapeShipCore
{
    class Program
    {
        const string SendDataConfigKey = "SendData";
        const string SendIntervalConfigKey = "SendInterval";
        const string LatitudeKey = "Latitude";
        const string LongitudeKey = "Longitude";

        static readonly Guid BatchId = Guid.NewGuid();
        static TimeSpan messageDelay;
        static bool sendData = true;
        static double latitude;
        static double longitude;
        static List<SensorData> sensorData = new List<SensorData>();

        static void Main(string[] args)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config/appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            messageDelay = configuration.GetValue("MessageDelay", TimeSpan.FromSeconds(5));

            var cts = new CancellationTokenSource();

            Init(cts).Wait();

            // Wait until the app unloads or is cancelled
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init(CancellationTokenSource cts)
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await ioTHubModuleClient.OpenAsync();

            // Execute callback method for Twin desired properties updates
            var twin = await ioTHubModuleClient.GetTwinAsync();
            await OnDesiredPropertiesUpdate(twin.Properties.Desired, ioTHubModuleClient);

            ModuleClient userContext = ioTHubModuleClient;
            await ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesUpdate, userContext);
            await ioTHubModuleClient.SetInputMessageHandlerAsync("inputFromSensors", SaveSensorData, ioTHubModuleClient);
            Console.WriteLine("IoT Hub module client initialized.");

            await SendEvents(ioTHubModuleClient, cts);
        }

        private static Task<MessageResponse> SaveSensorData(Message message, object userContext)
        {
            try
            {
                ModuleClient moduleClient = (ModuleClient)userContext;
                var messageBytes = message.GetBytes();
                var messageString = Encoding.UTF8.GetString(messageBytes);

                // Get the message body.
                var messageBody = JsonConvert.DeserializeObject<SensorData>(messageString);

                if (messageBody != null)
                {
                    sensorData.Add(messageBody);
                }

                // Indicate that the message treatment is completed.
                return Task.FromResult(MessageResponse.Completed);
            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error in sample: {0}", exception);
                }
                // Indicate that the message treatment is not completed.
                var moduleClient = (ModuleClient)userContext;
                return Task.FromResult(MessageResponse.Abandoned);
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error in sample: {0}", ex.Message);
                // Indicate that the message treatment is not completed.
                ModuleClient moduleClient = (ModuleClient)userContext;
                return Task.FromResult(MessageResponse.Abandoned);
            }
        }

        static async Task OnDesiredPropertiesUpdate(TwinCollection desiredPropertiesPatch, object userContext)
        {
            // At this point just update the configure configuration.
            if (desiredPropertiesPatch.Contains(LatitudeKey))
            {
                latitude = (double)desiredPropertiesPatch[LatitudeKey];
                Console.WriteLine($"Updated latitude, new value = {latitude}.");
            }

            if (desiredPropertiesPatch.Contains(LongitudeKey))
            {
                longitude = (double)desiredPropertiesPatch[LongitudeKey];
                Console.WriteLine($"Updated longitude, new value = {longitude}.");
            }

            if (desiredPropertiesPatch.Contains(SendIntervalConfigKey))
            {
                messageDelay = TimeSpan.FromSeconds((int)desiredPropertiesPatch[SendIntervalConfigKey]);
                Console.WriteLine($"Updated message delay, new value = {messageDelay}.");
            }

            if (desiredPropertiesPatch.Contains(SendDataConfigKey))
            {
                bool desiredSendDataValue = (bool)desiredPropertiesPatch[SendDataConfigKey];
                if (desiredSendDataValue != sendData && !desiredSendDataValue)
                {
                    Console.WriteLine("Sending data disabled. Change twin configuration to start sending again.");
                }

                sendData = desiredSendDataValue;
            }

            var moduleClient = (ModuleClient)userContext;
            var patch = new TwinCollection($"{{ \"{SendDataConfigKey}\": {sendData.ToString().ToLower()}, \"{SendIntervalConfigKey}\": {messageDelay.TotalSeconds} }}");
            await moduleClient.UpdateReportedPropertiesAsync(patch); // Just report back last desired property.
        }

        static async Task SendEvents(ModuleClient moduleClient, CancellationTokenSource cts)
        {
            int count = 0;

            while (!cts.Token.IsCancellationRequested)
            {
                if (sendData)
                {
                    var tempData = GetMessageBody();
                    string dataBuffer = JsonConvert.SerializeObject(tempData);
                    var eventMessage = new Message(Encoding.UTF8.GetBytes(dataBuffer));
                    eventMessage.Properties.Add("sequenceNumber", count.ToString());
                    eventMessage.Properties.Add("batchId", BatchId.ToString());
                    //Console.WriteLine($"\t{DateTime.Now.ToLocalTime()}> Sending message: {count}, Body: [{dataBuffer}]");

                    await moduleClient.SendEventAsync("sensorOutput", eventMessage);
                    count++;
                }

                await Task.Delay(messageDelay, cts.Token);
            }
        }

        private static MessageBody GetMessageBody()
        {
            var tempData = new MessageBody
            {
                Latitude = latitude,
                Longitude = longitude
            };

            try
            {
                var data = sensorData.Where(x => x.SensorType == SensorType.Temperature);
                if (data != null && data.Count() > 0)
                {
                    tempData.TemperatureDateTime = data.Last().DateTime;
                    tempData.Temperature = data.Last().Values.First();
                }

                data = sensorData.Where(x => x.SensorType == SensorType.Barometer);
                if (data != null && data.Count() > 0)
                {
                    tempData.PresureDateTime = data.Last().DateTime;
                    tempData.Presure = data.Last().Values.First();
                }

                data = sensorData.Where(x => x.SensorType == SensorType.Accelerometer);
                if (data != null && data.Count() > 0)
                {
                    tempData.AccelerometerDateTime = data.Last().DateTime;
                    tempData.Accelerometer = GetAxles(data.Last().Values);
                }

                data = sensorData.Where(x => x.SensorType == SensorType.Gyroscope);
                if (data != null && data.Count() > 0)
                {
                    tempData.GyroscopeDateTime = data.Last().DateTime;
                    tempData.Gyroscope = GetAxles(data.Last().Values);
                }

                data = sensorData.Where(x => x.SensorType == SensorType.Magnetometer);
                if (data != null && data.Count() > 0)
                {
                    tempData.MagnetometerDateTime = data.Last().DateTime;
                    tempData.Magnetometer = GetAxles(data.Last().Values);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while creating message body: {ex.Message}");
            }

            sensorData.Clear();

            return tempData;
        }

        private static Axles GetAxles(List<double> values)
        {
            return new Axles
            {
                XAxle = values[0],
                YAxle = values[1],
                ZAxle = values[2]
            };
        }
    }
}
