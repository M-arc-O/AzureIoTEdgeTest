using System;

namespace ShipShapeShipShared.Messages
{
    public class MessageBody
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime TemperatureDateTime { get; set; }
        public double Temperature { get; set; }
        public DateTime PresureDateTime { get; set; }
        public double Presure { get; set; }
        public DateTime GyroscopeDateTime { get; set; }
        public Axles Gyroscope { get; set; }
        public DateTime AccelerometerDateTime { get; set; }
        public Axles Accelerometer { get; set; }
        public DateTime MagnetometerDateTime { get; set; }
        public Axles Magnetometer { get; set; }
    }
}
