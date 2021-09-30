using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using ShipShapeShipShared;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShipShapeShipSensors
{
    class Program
    {
        static IConfiguration _configuration;
        static SerialPort _serialPort = new SerialPort();
        static ModuleClient _ioTHubModuleClient;

        const string GetSensorDataKey = "GetSensorData";
        const string PortNameKey = "PortName";
        const string BaudRateKey = "BaudRate";

        static bool _getSensorData;
        static string _portName;
        static int _baudRate;

        static void Main(string[] args)
        {
            _configuration = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("config/appsettings.json", optional: true)
               .AddEnvironmentVariables()
               .Build();

            _getSensorData = _configuration.GetValue("GetSensorData", true);
            _portName = _configuration.GetValue("SerialPort:PortName", "/dev/ttyAMA0");
            _baudRate = _configuration.GetValue("SerialPort:BaudRate", 9600);

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
            cancellationToken.Register(() => CloseSerialPort());
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
            _ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await _ioTHubModuleClient.OpenAsync();

            // Execute callback method for Twin desired properties updates
            var twin = await _ioTHubModuleClient.GetTwinAsync();
            await OnDesiredPropertiesUpdate(twin.Properties.Desired, _ioTHubModuleClient);

            ModuleClient userContext = _ioTHubModuleClient;
            await _ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesUpdate, userContext);
            Console.WriteLine("IoT Hub module client initialized.");

            SetupSerialPort();
        }

        static async Task OnDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            try
            {
                if (desiredProperties == null)
                {
                    return;
                }

                if (desiredProperties.Contains(GetSensorDataKey))
                {
                    _getSensorData = (bool)desiredProperties[GetSensorDataKey];
                    Console.WriteLine($"Updated get sensor data, new value = {_getSensorData}.");
                }

                if (desiredProperties.Contains(PortNameKey))
                {
                    _portName = (string)desiredProperties[PortNameKey];
                    Console.WriteLine($"Updated port name, new value = {_portName}.");
                    SetupSerialPort();
                }

                if (desiredProperties.Contains(BaudRateKey))
                {
                    _baudRate = (int)desiredProperties[BaudRateKey];
                    Console.WriteLine($"Updated get sensor data, new value = {_baudRate}.");
                    SetupSerialPort();
                }

                var moduleClient = (ModuleClient)userContext;
                var patch = new TwinCollection($"{{ \"{GetSensorDataKey}\": {_getSensorData.ToString().ToLower()}, \"{PortNameKey}\": \"{_portName.ToLower()}\", \"{BaudRateKey}\": {_baudRate} }}");
                await moduleClient.UpdateReportedPropertiesAsync(patch); // Just report back last desired property.
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while updating disered properties: {ex.Message}");
            }
        }

        private static void SetupSerialPort()
        {
            try
            {
                Console.WriteLine($"Try to open serial port: port name = {_portName}, baud rate = {_baudRate}");

                CloseSerialPort();

                _serialPort = new SerialPort
                {
                    PortName = _portName,
                    BaudRate = _baudRate,
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One
                };

                _serialPort.DataReceived += SerialPort_DataReceived;
                _serialPort.Open();

                Console.WriteLine($"Setup serial port: port name = {_portName}, baud rate = {_baudRate}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception message: {ex.Message}");
            }
        }

        private static void CloseSerialPort()
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
                _serialPort.DataReceived -= SerialPort_DataReceived;
                _serialPort.Dispose();
            }
        }

        private static void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = (SerialPort)sender;
            
            while (sp.BytesToRead > 0)
            {
                var input = sp.ReadLine();
                ParseSensorInput(input);
            }
        }

        private static async void ParseSensorInput(string input)
        {
            if (_getSensorData)
            {
                try
                {
                    var tempData = new SensorData
                    {
                        DateTime = DateTime.Now.ToLocalTime(),
                        Values = new List<double>()
                    };

                    var split = input.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);

                    tempData.SensorType = (SensorType)int.Parse(split[0]);

                    for (int i = 1; i < split.Length; i++)
                    {
                        tempData.Values.Add(double.Parse(split[i]));
                    }

                    string dataBuffer = JsonConvert.SerializeObject(tempData);
                    var eventMessage = new Message(Encoding.UTF8.GetBytes(dataBuffer));
                    // Console.WriteLine($"\t{tempData.DateTime}> Body: [{dataBuffer}]");

                    await _ioTHubModuleClient.SendEventAsync("sensorOutput", eventMessage);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Input = {input}");
                    Console.WriteLine(ex);
                }
            }
        }
    }
}
