using Azure.Messaging.EventHubs;
using EmailTelemetryProcessingService.Models;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Serialization.HybridRow.Schemas;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EmailTelemetryProcessingService
{
    public class EmailTelemetryEventHubTrigger
    {
        private readonly CosmosClient _cosmosClient;
        private readonly Database _database;
        private readonly Microsoft.Azure.Cosmos.Container _container;
        private string _databaseName;
        private string _containerName;
        private string _partitionKey;

        public EmailTelemetryEventHubTrigger(CosmosClient cosmosClient, IConfiguration configuration)
        {
            _cosmosClient = new CosmosClient(configuration["COSMOSDB_CONNECTION_STRING"]);
            _partitionKey = configuration["CampaignId"];
            _databaseName = configuration["DatabaseName"];
            _containerName = configuration["CollectionName"];
            _container= _cosmosClient.GetContainer(_databaseName, _containerName);
        }

        [FunctionName("EmailTelemetryEventHubTrigger")]
        public async Task Run([EventHubTrigger("email-telemetry-eventhub", Connection = "EVENT_HUB_CONNECTION_STRING")] EventData[] events, ILogger log)
        {
            var exceptions = new List<Exception>();

            foreach (EventData eventData in events)
            {
                try
                {
                    EventRecord eventRecord = JsonConvert.DeserializeObject<EventRecord>(Encoding.UTF8.GetString(eventData.EventBody));
                    foreach (Record record in eventRecord.records)
                    {
                        // Mail - Delivery Status Tracking
                        if (record.operationName.Equals("DeliveryStatusUpdate"))
                        {
                            log.LogInformation($"ACS Email - Correlation ID : {record.correlationId} - DeliveryStatus - {record?.properties.DeliveryStatus}");
                                                       
                            try
                            {
                                var response = await _container.ReadItemAsync<EmailListDto>(record.properties.RecipientId, new Microsoft.Azure.Cosmos.PartitionKey(_partitionKey));
                                var oldItem = response.Resource;

                                oldItem.Status = "Completed";

                                await _container.ReplaceItemAsync(oldItem, record.properties.RecipientId, new Microsoft.Azure.Cosmos.PartitionKey(_partitionKey));
                            }
                            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                            {
                                throw new Exception($"Item {record.properties.RecipientId} not found for this Campaign {_partitionKey}");
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
                throw new AggregateException(exceptions);

            if (exceptions.Count == 1)
                throw exceptions.Single();
        }
    }
}
