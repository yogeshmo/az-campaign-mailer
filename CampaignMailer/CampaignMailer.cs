using Azure.Communication.Email;
using CampaignList;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Azure;
using System.Threading;

namespace CampaignMailer
{
    public class CampaignMailer
    {
        private readonly IConfiguration _configuration;
        private readonly EmailClient emailClient;
        private readonly int _numRecipientsPerRequest;
        private readonly string campaignId;
        private readonly string storageConnectionString;

        public CampaignMailer(IConfiguration configuration)
        {
            emailClient = new EmailClient(configuration.GetConnectionStringOrSetting("ACSEmail"));
            campaignId = configuration.GetValue<string>("CampaignId");
            _numRecipientsPerRequest = configuration.GetValue<int>("NumRecipientsPerRequest");
            storageConnectionString = configuration.GetConnectionStringOrSetting("AzureBlobStorageConnection");
            _configuration = configuration;
        }

        /// <summary>
        /// HTTP trigger for the CampaignList Durable Function.
        /// </summary>
        /// <param name="req">The HTTPRequestMessage containing the request content.</param>
        /// <param name="client">The Durable Function orchestration client.</param>
        /// <param name="log">The logger instance used to log messages and status.</param>
        /// <returns>The URLs to check the status of the function.</returns>
        [FunctionName("CampaignMailerSBTrigger")]
        public async Task Run([ServiceBusTrigger("acsmails", Connection = "AzureServiceBus")] Message[] messageList, ILogger log)
        {
            log.LogInformation("Starting Sending Email Campaign");

            var tasks = new List<Task>();
            var recipientsList = new List<EmailAddress>();

            var emailBlobContent = await ReadEmailContentFromBlobStream(campaignId);

            var campaignContact = new CampaignContact
            {
                EmailContent = new EmailContent(emailBlobContent.MessageSubject)
                {
                    Html = emailBlobContent.MessageBodyHtml,
                    PlainText = emailBlobContent.MessageBodyPlainText
                },
                ReplyTo = new EmailAddress(emailBlobContent.ReplyToEmailAddress, emailBlobContent.ReplyToDisplayName),
                SenderEmailAddress = emailBlobContent.SenderEmailAddress
            };

            for (int i = 0; i < messageList.Length; i++)
            {
                var message = messageList[i];
                var messageBody = Encoding.UTF8.GetString(message.Body);
                var customer = JsonSerializer.Deserialize<EmailListDto>(messageBody);

                recipientsList.Add(new EmailAddress(customer.RecipientEmailAddress));
                log.LogInformation($"Adding {customer.RecipientEmailAddress}");

                if (recipientsList.Count == _numRecipientsPerRequest || (i == messageList.Length - 1))
                {
                    EmailRecipients recipients = new EmailRecipients(bcc: recipientsList);

                    tasks.Add(SendEmailAsync(campaignContact, recipients, log));

                    recipientsList.Clear();
                }
            }

            await Task.WhenAll(tasks);
        }

        private async Task SendEmailAsync(CampaignContact campaignContact, EmailRecipients recipients, ILogger log)
        {
            try
            {
                log.LogInformation("Sending email...");

                EmailMessage message = new EmailMessage(
                    campaignContact.SenderEmailAddress,
                    recipients,
                    campaignContact.EmailContent);

                message.Headers.Add("x-ms-acsemail-loadtest-skip-email-delivery", "ACS");

                var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

                EmailSendOperation emailSendOperation = await emailClient.SendAsync(Azure.WaitUntil.Started, message, cts.Token);

                /// Get the OperationId so that it can be used for tracking the message for troubleshooting
                string operationId = emailSendOperation.Id;
                log.LogInformation($"Email operation id = {operationId}");
            }
            catch (RequestFailedException ex)
            {
                /// OperationID is contained in the exception message and can be used for troubleshooting purposes
                log.LogError($"Email send operation failed with error code: {ex.ErrorCode}, message: {ex.Message}");
            }
            catch (OperationCanceledException ocex)
            {
                log.LogError($"Timeout Exception while sending email - {ocex}");
            }
            catch (Exception ex)
            {
                log.LogError($"Exception while sending email - {ex}");
            }
        }

        private CloudBlob GetBlobContent(string campaignId)
        {
            var blobName = campaignId + ".json";
            var blobContainerName = "campaigns";
            var blobConnectionString = storageConnectionString;// _configuration["AzureWebJobsStorage"];

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
                var blobContentDeSerialized = JsonSerializer.Deserialize<BlobDto>(blobContentSerializedString);
                return blobContentDeSerialized;
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
