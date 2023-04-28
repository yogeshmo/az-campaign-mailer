using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;

namespace CampaignMailer
{
    public class CampaignMailer
    {
        [FunctionName("CampaignMailer")]
        public void Run([ServiceBusTrigger("myqueue", Connection = "SBCONNSTR")] string myQueueItem, ILogger log)
        {
            Mailer.Initialize(log);
            CampaignContact campaignContact = null;

            try
            {
                campaignContact = JsonConvert.DeserializeObject<CampaignContact>(myQueueItem);
            }
            catch (Exception ex)
            {
                log.LogCritical($"Exception while attempting to deserialize {myQueueItem} - {ex}");
            }

            if (campaignContact != null)
            {
                //Mailer.SendMessage(campaignContact); 
            }
            else
            {
                log.LogError($"{myQueueItem} was serialized to null.");
            }
        }
    }
}
