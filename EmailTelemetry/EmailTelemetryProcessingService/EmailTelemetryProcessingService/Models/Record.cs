using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EmailTelemetryProcessingService.Models
{
    public class Record
    {
        public DateTime time { get; set; }
        public string resourceId { get; set; }
        public string location { get; set; }
        public string operationName { get; set; }
        public string operationVersion { get; set; }
        public string category { get; set; }
        public Properties properties { get; set; }
        public string correlationId { get; set; }
        public string resourceGroup { get; set; }
        public string resourceName { get; set; }
        public string resourceType { get; set; }
        public string subscriptionId { get; set; }
    }
}
