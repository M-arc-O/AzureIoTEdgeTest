using System;
using System.Collections.Generic;

namespace ShipShapeShipShared
{
    public class SensorData
    {
        public DateTime DateTime { get; set; }
        public SensorType SensorType { get; set; }
        public List<double> Values { get; set; }
    }
}
