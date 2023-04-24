using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CampaignEmailApp
{
    public class Campaign
	{
        [FunctionName("ProcessCampaignList")]
        public static async Task<IActionResult> Run(
           [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "campaign")] HttpRequest req,
           ILogger log)
        {
            log.LogInformation("Function: ProcessCampaignList Message: HTTP trigger function processed a request.");

            // Read the request body from the request
            /* Arguments: Stream, Encoding, detect encoding, buffer size
            var bodyStr = "";
            using (StreamReader reader = new StreamReader(req.Body, Encoding.UTF8, true, 1024, true))
            {
                bodyStr = reader.ReadToEnd();
            }

            // Parse the request body string into key/value pairs
            Dictionary<string, string> keyValuePairs = bodyStr.Split('&')
                .Select(value => value.Split('='))
                .ToDictionary(pair => pair[0], pair => pair[1]);

            // Read the query page size
            int pageSize = Int32.Parse(keyValuePairs["pageSize"]);

            // Read the campaign list name
            string listName = Regex.Replace(keyValuePairs["listName"], "%20", " ");

            // Read the campaign email message subject
            string msgSubject = Regex.Replace(keyValuePairs["msgSubject"], "%20", " ");

            // Read the campaign email HTML message body
            string msgBodyHtml = keyValuePairs["msgBodyHtml"];

            // Read the campaign email plain text message body
            string msgBodyPlainText = keyValuePairs["msgBodyPlainText"];
            */

            string requestBody = "";
            using (StreamReader streamReader = new StreamReader(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }
            CampaignConfiguration campaignConfig = JsonConvert.DeserializeObject<CampaignConfiguration>(requestBody);
            
            // Initialize the application
            CampaignList.Initialize(campaignConfig.PageSize, log);
            CampaignMailer.Initialize(campaignConfig.MsgSubject, campaignConfig.MsgBodyHtml, campaignConfig.MsgBodyPlainText, log);

            // Process the campaign list contact email messages
            CampaignList.Process(campaignConfig.ListName);

            // Return a response
            string responseMessage = string.IsNullOrEmpty(campaignConfig.ListName)
                ? "This HTTP triggered function executed successfully. Pass a campaign list name in the query string or in the request body for a personalized response."
                : $"Campaign List: {campaignConfig.ListName}. This HTTP triggered function executed successfully.";

            return new OkObjectResult(responseMessage);
        }
    }
}
