using Azure.Messaging.EventHubs;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Serialization.HybridRow.Schemas;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EmailTelemetryProcessingService
{
    public class EmailTelemetryEventHubTrigger
    {
        private readonly CosmosClient _cosmosClient;
        private readonly Database _database;
        private readonly Container _container;
        private string _databaseName;
        private string _containerName;

        public EmailTelemetryEventHubTrigger(CosmosClient cosmosClient, IConfiguration configuration)
        {
            _cosmosClient = new CosmosClient(configuration["COSMOSDB_CONNECTION_STRING"]);
            _databaseName = configuration["DatabaseName"];
            _containerName = configuration["CollectionName"];
        }


        [FunctionName("EmailTelemetryEventHubTrigger")]
        public async Task Run([EventHubTrigger("email-telemetry-eventhub", Connection = "EVENT_HUB_CONNECTION_STRING")] EventData[] events, ILogger log)
        {
            var exceptions = new List<Exception>();

            foreach (EventData eventData in events)
            {
                try
                {
                    var container = _cosmosClient.GetContainer(_databaseName, _containerName);

                    try
                    {
                        var response = await container.ReadItemAsync<dynamic>(id, new PartitionKey(partitionKey));
                        var oldItem = response.Resource;

                        oldItem.Property1 = item.Property1;
                        oldItem.Property2 = item.Property2;

                        await container.ReplaceItemAsync(oldItem, id, new PartitionKey(partitionKey));
                    }
                    catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        throw new Exception($"Item {id} not found");
                    }
                }
                catch (Exception e)
                {
                    // We need to keep processing the rest of the batch - capture this exception and continue.
                    // Also, consider capturing details of the message that failed processing so it can be processed again later.
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
