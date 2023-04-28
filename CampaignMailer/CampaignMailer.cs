using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Net.Http;
using System.Threading.Tasks;

namespace CampaignMailer
{
    public class CampaignMailer
    {
        private readonly CosmosClient _cosmosClient;
        private readonly Database _database;
        private readonly Microsoft.Azure.Cosmos.Container _container;
        private string _databaseName;
        private string _containerName;
        private readonly int _numRecipientsPerRequest = 50;

        public CampaignMailer(CosmosClient cosmosClient, IConfiguration configuration)
        {
            _cosmosClient = new CosmosClient(configuration["COSMOSDB_CONNECTION_STRING"]);
            _databaseName = configuration["DatabaseName"];
            _containerName = configuration["CollectionName"];
            _container = _cosmosClient.GetContainer(_databaseName, _containerName);
        }

        /// <summary>
        /// HTTP trigger for the CampaignList Durable Function.
        /// </summary>
        /// <param name="req">The HTTPRequestMessage containing the request content.</param>
        /// <param name="client">The Durable Function orchestration client.</param>
        /// <param name="log">The logger instance used to log messages and status.</param>
        /// <returns>The URLs to check the status of the function.</returns>
        [FunctionName("CampaignMailerHttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
           [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "campaign")] HttpRequestMessage req,
           [DurableClient] IDurableOrchestrationClient client,
           ILogger log)
        {
            // Get the campaign information from the HTTP request body
            CampaignRequest campaignRequest = await req.Content.ReadAsAsync<CampaignRequest>();

            // Function input comes from the request content.
            string instanceId = await client.StartNewAsync("CampaignListOrchestrator", campaignRequest);

            log.LogInformation($"Started orchestration with ID = '{instanceId}", instanceId);

            // Create the URL to allow the client to check status of a request (excluding the function key in the code querystring)
            string checkStatusUrl = string.Format("{0}://{1}:{2}/campaign/CampaignListHttpStart_Status?id={3}", req.RequestUri.Scheme, req.RequestUri.Host, req.RequestUri.Port, instanceId);

            // Create the response and add headers
            var response = new HttpResponseMessage()
            {
                StatusCode = System.Net.HttpStatusCode.Accepted,
                Content = new StringContent(checkStatusUrl),
            };
            response.Headers.Add("Location", checkStatusUrl);
            response.Headers.Add("Retry-After", "10");

            return response;
        }


        [FunctionName("CampaignListOrchestrator")]
        public async Task RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            // [ServiceBus("myqueue", Connection = "ServiceBusConn")] IAsyncCollector<string> queueContacts,
            ILogger log)
        {
            Mailer.Initialize(log);

            var campaignRequest = context.GetInput<CampaignRequest>();

            try
            {
                var query = new QueryDefinition("SELECT * FROM c WHERE c.partitionKey = @partitionKey")
                    .WithParameter("@partitionKey", campaignRequest.CampaignId);

                using var resultSetIterator = _container.GetItemQueryIterator<EmailListDto>(query);
                while (resultSetIterator.HasMoreResults)
                {
                    int count = 0;
                    var currentResultSet = await resultSetIterator.ReadNextAsync();
                    CampaignContact campaignContact = null;
                    foreach (var item in currentResultSet)
                    {
                        if (count == 0)
                        {
                            campaignContact = new CampaignContact()
                            {
                                SenderEmailAddress = "",    // TODO:Get sender address from BlobDto
                            };
                        }

                        campaignContact.EmailAddresses.Add(new Azure.Communication.Email.EmailAddress(item.RecipientEmailAddress, item.RecipientFullName));
                        count++;

                        if (count == _numRecipientsPerRequest)
                        {
                            // send email
                           await Mailer.SendAsync(campaignContact);

                            campaignContact = null;
                            count = 0;
                            log.LogInformation($"Processing email record for {_numRecipientsPerRequest} recipients");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogError($"orchestrator failed with exception {ex}");
            }
        }
    }
}
