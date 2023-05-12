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
        public EmailContent EmailContent { get; set; }

        public EmailAddress ReplyTo { get; set; }

        public string SenderEmailAddress { get; set; }
    }
}
