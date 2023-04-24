using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;


namespace CampaignEmailApp
{
    public class CampaignMailer
    {
        // Subject of the campaign email message
        private static string msgSubject;

        // Body of the email HTML message
        private static string msgBodyHtml;

        // Body of the email plain text message
        private static string msgBodyPlainText;

        // ACS email client used to send email messages
        private static EmailClient emailClient;

        // ACS Email Communications Service domain sender email address
        private static string domainSenderAddress;

        // Application logger to log application status, messages, and errors
        private static ILogger logger;


        /// <summary>
        /// Initialize the static class to the Azure Communication Services instance
        /// </summary>
        /// <param name="log"></param>
        public static void Initialize(string emailSubject, string emailBodyHtml, string emailBodyPlainText, ILogger appLogger) 
        {
            // Keep the app logger for use in the methods
            logger = appLogger;

            // Capture the subject and body of the campaign email message
            msgSubject = emailSubject;
            msgBodyHtml = emailBodyHtml;
            msgBodyPlainText = emailBodyPlainText;
            logger.LogInformation($"Message Subject: {msgSubject} Message Body: {msgBodyPlainText}");

            // Create the email client using the connection string in the function properties
            string connectionString = Environment.GetEnvironmentVariable("COMMUNICATION_SERVICES_CONNECTION_STRING");
            emailClient = new EmailClient(connectionString);

            // Retrieve the ACS email communication service domain sender email address from the function properties
            domainSenderAddress = Environment.GetEnvironmentVariable("COMMUNICATION_SERVICES_DOMAIN_SENDER_ADDRESS");
        }

        public static void SendMessage(IEnumerable contacts)
        {
            try 
            {
                // Create the email content - subject and email message
                var emailContent = new EmailContent(msgSubject)
                {
                    PlainText = msgBodyPlainText,
                    Html = msgBodyHtml
                };

                // Create the email message TO distribution list
                List<EmailAddress> toEmailAddresses = new List<EmailAddress>();
                EmailAddress toEmailAddr = new EmailAddress(domainSenderAddress, "Do Not Reply");
                toEmailAddresses.Add(toEmailAddr);

                // Loop through the campaign contacts to create the BCC distribution list
                List<EmailAddress> bccEmailAddresses = new List<EmailAddress>();
                foreach (CampaignContact contact in contacts)
                {
                    EmailAddress bccEmailAddr = new EmailAddress(contact.EmailAddress, contact.FullName);
                    //EmailAddress bccEmailAddr = new EmailAddress("loublick@microsoft.com") { DisplayName = "Lou Blick" };
                    bccEmailAddresses.Add(bccEmailAddr);
                }
                var emailRecipients = new EmailRecipients(toEmailAddresses, toEmailAddresses, bccEmailAddresses);
                EmailMessage emailMessage = new EmailMessage(domainSenderAddress, emailRecipients, emailContent);
                EmailSendOperation emailSendOp = emailClient.Send(WaitUntil.Completed, emailMessage, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogInformation($"CampaignMailer.SendMessage");
                logger.LogInformation($"{ex.Message}");
            }
        }
    }
}
