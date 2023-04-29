using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EmailTelemetryProcessingService.Models
{
    public class Record
    {
        public string Sender { get; set; }
        public string Recipient { get; set; }
        public string MessageId { get; set; }
        public string Status { get; set; }
        public DateTime DeliveryAttemptTimestamp { get; set; }
    }
}
