using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EmailTelemetryProcessingService.Models
{
    public class Properties
    {
        public string OperationType { get; set; }
        public string OperationCategory { get; set; }
        public string RecipientId { get; set; }
        public string EngagementType { get; set; }
        public string EngagementContext { get; set; }
        public string UserAgent { get; set; }
        public string DeliveryStatus { get; set; }
    }
}
