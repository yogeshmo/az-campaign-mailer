/*
 * CampaignList function helper class.
 */
namespace CampaignEmailApp
{
    /// <summary>
    /// Campaign configuration data used to encapsulate the HTTP message body data.
    /// </summary>
    public class CampaignConfiguration
    {
        public int PageSize { get; set; }

        public string ListName { get; set; }

        public string MsgSubject { get; set; }

        public string MsgBodyHtml { get; set; }

        public string MsgBodyPlainText { get; set; }

        public string FromAddress { get; set; }
    }
}