using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.EventGrid;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EventGridTest
{
    public static class PublishEvents
    {
        static EventGridClient client;
        static string topicHostname;

        [FunctionName("PublishEvents")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "publishevents")] HttpRequest req,
            ILogger log)
        {
            InitializeEventGridClient();

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            int totalThreads = data?.threadCount;
            int perThreadCount = data?.perThreadCount;
            var tasks = new List<Task>();

            for (int i = 0; i < totalThreads; i++)
            {
                tasks.Add(RunThread(perThreadCount, log));
            }

            await Task.WhenAll(tasks);

            return new OkObjectResult("");
        }

        private static async Task RunThread(int perThreadCount, ILogger log)
        {
            List<EventGridEvent> events = Enumerable.Range(0, perThreadCount)
                .Select(i => new EventGridEvent()
                {
                    Id = Guid.NewGuid().ToString(),
                    EventType = "Microsoft.Communication.EmailDeliveryReportReceived",
                    Data = new EmailEventData
                    {
                        Sender = "DoNotReply@db0fea4e-c42b-4dc6-a57e-f4a30e831342.us1.azurecomm.net",
                        Recipient = "rajatthegr8@gmail.com",
                        MessageId = $"{Guid.NewGuid()}",
                        Status = "Delivered",
                        DeliveryAttemptTimestamp = DateTime.Now
                    },
                    EventTime = DateTime.Now,
                    Subject = $"sender/DoNotReply@db0fea4e-c42b-4dc6-a57e-f4a30e831342.us1.azurecomm.net/message/{i}",
                    DataVersion = "1.0",
                })
                .ToList();

            try
            {
                await client.PublishEventsAsync(topicHostname, events);
            }
            catch (Exception ex)
            {
                log.LogError($"{ex}");
            }
        }

        private static void InitializeEventGridClient()
        {
            string topicEndpoint = Environment.GetEnvironmentVariable("TOPIC_ENDPOINT");
            string topicKey = Environment.GetEnvironmentVariable("TOPIC_KEY");

            topicHostname = new Uri(topicEndpoint).Host;
            TopicCredentials topicCredentials = new(topicKey);
            client = new EventGridClient(topicCredentials);
        }
    }
}
