// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using Azure.Data.Tables;
using Azure.Messaging.EventGrid;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EventGridTest
{
    public class ProcessEvents
    {
        static TableClient tableClient;

        [FunctionName("ProcessEvents")]
        public static async Task Run([EventGridTrigger] EventGridEvent[] eventGridEvents, ILogger log)
        {
            log.LogInformation($"[EventProcessor] Received {eventGridEvents.Length} events.");

            await InitializeTableClientAsync(log);

            if (tableClient != null)
            {
                var tasks = new List<Task<OperationStatusEntity>>();
                try
                {
                    foreach (var eventGridEvent in eventGridEvents)
                    {
                        tasks.Add(ProcessEventAsync(eventGridEvent, log));
                    }
                    OperationStatusEntity[] entities = await Task.WhenAll(tasks);
                    List<TableTransactionAction> batch = new();
                    batch.AddRange(entities.Select(entity => new TableTransactionAction(TableTransactionActionType.UpsertMerge, entity)));

                    if (batch.Count > 0)
                    {
                        await tableClient.SubmitTransactionAsync(batch);
                    }

                    log.LogInformation($"[EventProcessor] Successfully processed {eventGridEvents.Length} events.");
                }
                catch (Exception ex)
                {
                    log.LogError($"[EventProcessor] {ex}");
                    throw;
                }
            }
        }

        private static Task<OperationStatusEntity> ProcessEventAsync(EventGridEvent eventGridEvent, ILogger log)
        {
            try
            {
                if (eventGridEvent != null && eventGridEvent.Data != null)
                {
                    var emailEventData = JsonConvert.DeserializeObject<EmailEventData>(eventGridEvent.Data.ToString());

                    var entity = new OperationStatusEntity
                    {
                        PartitionKey = emailEventData.Recipient,
                        RowKey = emailEventData.MessageId,
                        Status = emailEventData.Status
                    };

                    return Task.FromResult(entity);
                }
            }
            catch (Exception ex)
            {
                log.LogError("[EventProcessor]" + ex.ToString());
                throw;
            }

            return null;
        }

        private static async Task InitializeTableClientAsync(ILogger log)
        {
            if (tableClient == null)
            {
                try
                {
                    var serviceClient = new TableServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
                    tableClient = serviceClient.GetTableClient("OperationStatus");
                    await tableClient.CreateIfNotExistsAsync();
                }
                catch (Exception ex)
                {
                    log.LogError($"[EventProcessor] {ex}");
                }
            }
        }
    }
}