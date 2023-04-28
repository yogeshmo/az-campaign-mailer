using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CampaignMailer
{
    public class CampaignRequest
    {
        [JsonProperty("campaignId", Required = Required.Always)]
        public string CampaignId { get; set; }

        [JsonProperty("sendOnlyToNewRecipients")]
        public bool SendOnlyToNewRecipients { get; set; } = true;
    }
}
