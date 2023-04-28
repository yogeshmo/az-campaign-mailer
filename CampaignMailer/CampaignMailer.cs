using Azure.Communication.Email;
using CampaignList;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CampaignMailer
{
    public class CampaignMailer
    {
        private readonly CosmosClient _cosmosClient;
        private readonly Microsoft.Azure.Cosmos.Container _container;
        private string _databaseName;
        private string _containerName;
        private readonly IConfiguration _configuration;

        private readonly int _numRecipientsPerRequest = 50;

        public CampaignMailer(IConfiguration configuration)
        {
            _configuration = configuration;
            _cosmosClient = new CosmosClient(configuration["COSMOSDB_CONNECTION_STRING"]);
            _databaseName = "Campaign";
            _containerName = "EmailList";
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
           [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "startCampaignMailer")] HttpRequestMessage req,
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
            ILogger log)
        {
            Mailer.Initialize(log);

            var campaignRequest = context.GetInput<CampaignRequest>();
            var emailContent = await ReadEmailContentFromBlobStream(campaignRequest.CampaignId);

            try
            {
                var queryString = string.Empty;
                if (campaignRequest.SendOnlyToNewRecipients)
                {
                    queryString = $"SELECT * FROM c WHERE c.partitionKey = '{campaignRequest.CampaignId}' AND c.status = 'NotStarted'";
                }
                else
                {
                    queryString = $"SELECT * FROM c WHERE c.partitionKey = '{campaignRequest.CampaignId}' AND c.status = 'InProgress'";
                }

                var query = new QueryDefinition(queryString);

                using var resultSetIterator = _container.GetItemQueryIterator<EmailListDto>(queryString);
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
                                SenderEmailAddress = emailContent.SenderEmailAddress,
                                MessageBodyHtml = emailContent.MessageBodyHtml,
                                MessageSubject = emailContent.MessageSubject,
                                MessageBodyPlainText = emailContent.MessageBodyPlainText,
                                ReplyToDisplayName = emailContent.ReplyToDisplayName,
                                ReplyToEmailAddress = emailContent.ReplyToEmailAddress,
                            };
                        }

                        campaignContact.EmailAddresses.Add(new EmailAddress(item.RecipientEmailAddress, string.Empty));
                        count++;

                        if (count == _numRecipientsPerRequest)
                        {
                            // send email
                            await UpdateStatusInCosomsDBForRecipients(campaignContact.EmailAddresses, campaignRequest.CampaignId);
                            // await Mailer.SendAsync(campaignContact);

                            campaignContact = null;
                            count = 0;
                            log.LogInformation($"Processing email record for {_numRecipientsPerRequest} recipients");
                        }
                    }

                    if (count < _numRecipientsPerRequest)
                    {
                        // send email
                        await UpdateStatusInCosomsDBForRecipients(campaignContact.EmailAddresses, campaignRequest.CampaignId);
                        // await Mailer.SendAsync(campaignContact);
                        log.LogInformation($"Processing email record for {count} recipients");
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogError($"orchestrator failed with exception {ex}");
            }
        }

        private async Task UpdateStatusInCosomsDBForRecipients(List<EmailAddress> recipients, string campaignId)
        {
            List<Task> updateDbTasks = new List<Task>();
            foreach (var recipient in recipients)
            {
                var emailListDto = new EmailListDto()
                {
                    RecipientEmailAddress = recipient.Address,
                    RecipientFullName = recipient.DisplayName,
                    Status = DeliveryStatus.InProgress.ToString(),
                    CampaignId = campaignId,
                };

                updateDbTasks.Add(_container.UpsertItemAsync(emailListDto, new PartitionKey(emailListDto.CampaignId)));
            }

            await Task.WhenAll(updateDbTasks);
        }

        private CloudBlob GetBlobContent(string campaignId)
        {
            var blobName = campaignId + ".json";
            var blobContainerName = "campaigns";
            var blobConnectionString = _configuration["AzureWebJobsStorage"];

            // Create a CloudStorageAccount object from the connection string
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(blobConnectionString);

            // Create a CloudBlobClient object from the storage account
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            // Retrieve a reference to the blob container
            CloudBlobContainer container = blobClient.GetContainerReference(blobContainerName);
            var blobContent = container.GetBlobReference(blobName);

            return blobContent;
        }

        private async Task<BlobDto> ReadEmailContentFromBlobStream(string campaignId)
        {
            var blobContent = GetBlobContent(campaignId);
            using var stream = new MemoryStream();
            await blobContent.DownloadToStreamAsync(stream);

            // Return a response indicating success
            var blobContentSerializedString = Encoding.UTF8.GetString(stream.ToArray());

            try
            {
                var blobContentDeSerialized = JsonConvert.DeserializeObject<BlobDto>(blobContentSerializedString);
                return blobContentDeSerialized;
            }
            catch (Exception ex)
            {
                // log error
                return null;
            }
        }
    }
}
