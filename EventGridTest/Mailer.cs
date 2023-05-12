using Azure;
using Azure.Communication.Email;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace EventGridTest
{
    internal class Mailer
    {
        [FunctionName("MailerHttpStart")]
        public static async Task<IActionResult> HttpStart(
           [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "startMailer")] HttpRequestMessage req,
           ILogger log)
        {
            string opId = await SendAsync(log);
            return new OkObjectResult(opId);
        }

        private static async Task<string> SendAsync(ILogger logger)
        {

            // Create the email content - subject and email message
            try
            {
                var emailContent = new EmailContent("TestSubject")
                {
                    PlainText = "Testing 123"
                };

                // Create the email message TO distribution list

                EmailRecipients emailRecipients = new(new List<EmailAddress> { new EmailAddress("rajatthegr8@gmail.com") });
                EmailMessage emailMessage = new("DoNotReply@db0fea4e-c42b-4dc6-a57e-f4a30e831342.azurecomm.net", emailRecipients, emailContent);

                string connectionString = Environment.GetEnvironmentVariable("COMMUNICATION_SERVICES_CONNECTION_STRING");
                var emailClient = new EmailClient(connectionString);
                EmailSendOperation emailSendOp = await emailClient.SendAsync(WaitUntil.Started, emailMessage);
                logger.LogInformation($"Email sent to Tracking Operation Id: {emailSendOp.Id}");
                return emailSendOp.Id;

            }
            catch (RequestFailedException rfex)
            {
                logger.LogCritical($"Failed to deliver email Request failed exception - {rfex}");
            }
            catch (OperationCanceledException ocex)
            {
                logger.LogError($"Timeout Exception while sending email - {ocex}");
            }
            catch (Exception ex)
            {
                logger.LogError($"Exception while sending email - {ex}");
            }

            return string.Empty;
        }
    }
}
