using Newtonsoft.Json;

namespace CampaignList
{
    public class EmailListDto
    {
        [JsonProperty("campaignId")]
        public string CampaignId { get; set; }

        [JsonProperty("id")]
        public string RecipientEmailAddress { get; set; }

        [JsonProperty("recipientFullName")]
        public string RecipientFullName { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("operationId")]
        public string OperationId { get; set; }
    }
}
