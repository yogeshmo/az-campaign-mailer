using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;

namespace CampaignMailer
{
    internal class Mailer
    {
        // ACS email client used to send email messages
        private static EmailClient emailClient;

        // Application logger to log application status, messages, and errors
        private static ILogger logger;

        public static void Initialize(ILogger appLogger)
        {
            // Keep the app logger for use in the methods
            logger = appLogger;

            // Create the email client using the connection string in the function properties
            string connectionString = Environment.GetEnvironmentVariable("COMMUNICATION_SERVICES_CONNECTION_STRING");
            emailClient = new EmailClient(connectionString);
        }


        public static async Task SendAsync(CampaignContact campaignContact)
        {

            // Create the email content - subject and email message
            try
            {
                var emailContent = new EmailContent(campaignContact.MessageSubject)
                {
                    PlainText = campaignContact.MessageBodyPlainText,
                    Html = campaignContact.MessageBodyHtml,
                };

                // Create the email message TO distribution list

                var emailRecipients = new EmailRecipients(null, null, campaignContact.EmailAddresses);

                EmailMessage emailMessage = new(campaignContact.SenderEmailAddress, emailRecipients, emailContent);
                emailMessage.ReplyTo.Add(new EmailAddress(campaignContact.ReplyToEmailAddress, campaignContact.ReplyToDisplayName));

                var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

                // Uncomment the below for a real execution
                EmailSendOperation emailSendOp = await emailClient.SendAsync(WaitUntil.Started, emailMessage, cts.Token);
                logger.LogInformation($"Email sent to Tracking Operation Id: {emailSendOp.Id}");

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

        }
    }
}
