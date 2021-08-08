using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartGreenhouse.MonitoringService
{
    public class ConditionsReading
    {
        public double AirTemperature { get; set; }
        public double AirHumidity { get; set; }
        public double SoilMoisture { get; set; }
        public int LightIntensity { get; set; }
        public DateTime TimeStamp { get; set; }
        public string SensorNodeId { get; set; }
    }
}
