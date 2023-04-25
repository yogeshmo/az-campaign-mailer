using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;

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

        public static void SendMessage(CampaignContact campaignContact)
        {
            try
            {
                // Create the email content - subject and email message
                var emailContent = new EmailContent(campaignContact.MessageSubject)
                {
                    PlainText = campaignContact.MessageBodyPlainText,
                    Html = campaignContact.MessageBodyHtml
                };

                EmailMessage emailMessage = new(campaignContact.SenderEmailAddress, campaignContact.EmailAddress, emailContent);

                // *********************TO BE REMOVED**************************
                // Mark this email as ACS internal email to skip email delivery
                emailMessage.Headers.Add("x-ms-acsemail-loadtest-skip-email-delivery", "ACS");

                EmailSendOperation emailSendOp = emailClient.Send(WaitUntil.Started, emailMessage, CancellationToken.None);

                logger.LogInformation($"Operation Id: {emailSendOp.Id}");
            }
            catch (Exception ex)
            {
                logger.LogInformation($"Exception while sending email - {ex}");
            }
        }
    }
}
