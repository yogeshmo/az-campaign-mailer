/*
 * CampaignList Durable Function helper class.
 */
using Azure.Communication.Email;
using System.Collections.Generic;

namespace CampaignMailer
{
    /// <summary>
    /// Campaign contact information class used to pass campaign and member contact
    /// data to CampaignMailer Azure functions.
    /// </summary>
    /// </summary>
    public class CampaignContact
    {
        public List<EmailAddress> EmailAddresses { get; set; } = new List<EmailAddress>();  // email address, full name tuple

        public string ReplyToEmailAddress { get; set; }



        public string ReplyToDisplayName { get; set; }



        public string MessageSubject { get; set; }



        public string MessageBodyHtml { get; set; }



        public string MessageBodyPlainText { get; set; }



        public string SenderEmailAddress { get; set; }
    }
}
