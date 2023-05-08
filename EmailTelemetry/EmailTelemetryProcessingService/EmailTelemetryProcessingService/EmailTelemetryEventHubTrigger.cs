using EmailTelemetryProcessingService.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EmailTelemetryProcessingService
{
    public class EmailTelemetryEventHubTrigger
    {
        private readonly CosmosClient _cosmosClient;
        private readonly Container _container;
        private readonly string _partitionKey;

        public EmailTelemetryEventHubTrigger(IConfiguration configuration)
        {
            _cosmosClient = new CosmosClient(configuration["COSMOSDB_CONNECTION_STRING"]);
            _partitionKey = "newslettercampaign";
            _container = _cosmosClient.GetContainer("Campaign", "EmailList");
        }

        [FunctionName("EmailTelemetryEventHubTrigger")]
        public async Task Run([EventHubTrigger("email-telemetry-eventhub", Connection = "EVENT_HUB_CONNECTION_STRING")] string[] eventMessages, ILogger log)
        {
            var exceptions = new List<Exception>();

            foreach (var eventMessage in eventMessages)
            {
                try
                {
                    var eventRecords = JsonConvert.DeserializeObject<List<EventRecord>>(eventMessage);
                    foreach (var eventRecord in eventRecords)
                    {
                        // Mail - Delivery Status Tracking
                        if (eventRecord.EventType.Equals("Microsoft.Communication.EmailDeliveryReportReceived"))
                        {
                            log.LogInformation($"ACS Email - Correlation ID : {eventRecord.Data.MessageId} - DeliveryStatus - {eventRecord.Data.Status}");

                            try
                            {
                                var response = await _container.ReadItemAsync<EmailListDto>(eventRecord.Data.Recipient, new PartitionKey(_partitionKey));
                                var oldItem = response.Resource;

                                oldItem.Status = "Completed";

                                await _container.ReplaceItemAsync(oldItem, eventRecord.Data.Recipient, new PartitionKey(_partitionKey));
                            }
                            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                            {
                                throw new Exception($"Item {eventRecord.Data.Recipient} not found for this Campaign {_partitionKey}");
                            }
                        }

                    }
                    await Task.Yield();
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }

            // Once processing of the batch is complete, if any messages in the batch failed processing throw an exception so that there is a record of the failure.

            if (exceptions.Count > 1)
            {
                var ex = new AggregateException(exceptions);
                log.LogError($"{ex}");
                throw ex;
            }

            if (exceptions.Count == 1)
            {
                var ex = exceptions.Single();
                log.LogError($"{ex}");
                throw ex;
            }
        }
    }
}
