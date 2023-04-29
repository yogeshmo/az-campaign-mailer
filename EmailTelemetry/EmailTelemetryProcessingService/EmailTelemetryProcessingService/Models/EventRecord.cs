using Microsoft.Azure.Amqp.Framing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EmailTelemetryProcessingService.Models
{
    public class EventRecord
    {
        public string Id { get; set; }
        public string Topic { get; set; }
        public string Subject { get; set; }
        public Record Data { get; set; }
        public string EventType { get; set; }
        public string DataVersion { get; set; }
        public string MetadataVersion { get; set; }
        public DateTime EventTime { get; set; }
    }
}
