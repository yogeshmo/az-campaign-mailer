using System;

namespace CampaignEmailApp
{
    internal class CampaignConfiguration
    {
        private int pageSize;
        private string listName;
        private string msgSubject;
        private string msgBodyHtml;
        private string msgBodyPlainText;

        public int PageSize { get => pageSize; set => pageSize = value; }
        public string ListName { get => listName; set => listName = value; }
        public string MsgSubject { get => msgSubject; set => msgSubject = value; }
        public string MsgBodyHtml { get => msgBodyHtml; set => msgBodyHtml = value; }
        public string MsgBodyPlainText { get => msgBodyPlainText; set => msgBodyPlainText = value; }
    }
}
