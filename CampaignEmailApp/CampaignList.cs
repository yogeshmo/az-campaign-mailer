using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace CampaignEmailApp
{
    public static class CampaignList
    {
        // Paging size of the campaign list
        private static int listPageSize;

        // Client used to access a Dataverse database
        private static ServiceClient serviceClient = null;

        // Application logger to log application status, messages, and errors
        private static ILogger logger;

        /// <summary>
        /// Manages authentication and initialization of Dataverse Service Client
        /// </summary>
        public static void Initialize(int pageSize, ILogger appLogger)
        {
            // Keep the logger for use in all methods
            logger = appLogger;

            // Set the page size
            if (pageSize > 0)
            {
                listPageSize = pageSize;
            }
            else
            {
                listPageSize = 5000;
            }

            // Dataverse environment URL and login info.
            string url = Environment.GetEnvironmentVariable("DATAVERSE_URL");
            string appId = Environment.GetEnvironmentVariable("DATAVERSE_APPID");
            string secret = Environment.GetEnvironmentVariable("DATAVERSE_SECRET");

            // Create the Dataverse connection string
            string connectionString =
                $@"AuthType=ClientSecret;
                SkipDiscovery=true;url={url};
                Secret={secret};
                ClientId={appId};
                RequireNewInstance=true";

            try
            {
                // Create the Dataverse ServiceClient instance
                serviceClient = new(connectionString);
                logger.LogInformation("Successfully created the Dataverse service client");
            }
            catch (Exception ex) 
            {
                logger.LogInformation($"Exception thrown: {ex.Message}");
            }
        }

        public static void Process(string listName)
        {
            if (listName.Length > 0)
            {
                using (serviceClient)
                {
                    if (serviceClient.IsReady)
                    {
                        if (IsDynamic(listName, serviceClient))
                        {
                            QueryDynamicCampaignList(listName, serviceClient);
                        }
                        else
                        {
                            var listId = LookupCampaignListID(listName, serviceClient);
                            QueryStaticCampaignList(listId, serviceClient);
                        }
                        logger.LogInformation($"Successfully completed processing {listName}");
                    }
                    else
                    {
                        logger.LogInformation($"A web service connection was not established. Campaign list {listName} was not processed");
                    }
                }
            }
            else
            {
                logger.LogInformation("Campaign list name was empty. Please provide the name of a campaign list to process.");
            }
        }

        public static bool IsDynamic(string listName, ServiceClient serviceClient)
        {
            // Query a campaign name
            var query = new QueryByAttribute("list")
            {
                ColumnSet = new ColumnSet("type")
            };
            query.AddAttributeValue("listname", listName);

            var results = serviceClient.RetrieveMultiple(query);
            return bool.Parse(results.Entities.First().Attributes["type"].ToString());
        }

        public static void QueryDynamicCampaignList(string listName, ServiceClient serviceClient)
        {

            // Query a campaign name
            var query = new QueryByAttribute("list")
            {
                ColumnSet = new ColumnSet("query")
            };
            query.AddAttributeValue("listname", listName);

            var results = serviceClient.RetrieveMultiple(query);
            var fetchXml = results.Entities.First().Attributes["query"].ToString();
            fetchXml = EnsureContactRecordHasEmailAttribute(fetchXml);

            // Define the fetch attributes.
            // Set the number of records per page to retrieve.
            int fetchCount = listPageSize;

            // Initialize the page number.
            int pageNumber = 1;

            // Initialize the number of records.
            int recordCount = 0;

            // Specify the current paging cookie. For retrieving the first page, 
            // pagingCookie should be null.
            string pagingCookie = null;

            while (true)
            {
                // Build fetchXml string with the placeholders.
                string xml = CreatePaginatedXml(fetchXml, pagingCookie, pageNumber, fetchCount);

                // Excute the fetch query and get the xml result.
                RetrieveMultipleRequest fetchRequest1 = new RetrieveMultipleRequest
                {
                    Query = new FetchExpression(xml)
                };

                EntityCollection returnCollection = ((RetrieveMultipleResponse)serviceClient.Execute(fetchRequest1)).EntityCollection;

                List<CampaignContact> campaignContacts= new List<CampaignContact>();
                foreach (var c in returnCollection.Entities)
                {
                    var campaignContact = new CampaignContact()
                    {
                        EmailAddress = c.Attributes["emailaddress1"].ToString(),
                        FullName = c.Attributes["fullname"].ToString()
                    };

                    campaignContacts.Add(campaignContact);
                    recordCount++;

                    System.Console.WriteLine("{0}.\t{1}\t\t{2}",
                        ++recordCount,
                        campaignContact.FullName,
                        campaignContact.EmailAddress);

                }
                CampaignMailer.SendMessage(campaignContacts);

                // Check for more records, if it returns 1.
                if (returnCollection.MoreRecords)
                {
                    Console.WriteLine("\n****************\nPage number {0}\n****************", pageNumber);

                    // Increment the page number to retrieve the next page.
                    pageNumber++;

                    // Set the paging cookie to the paging cookie returned from current results.                            
                    pagingCookie = returnCollection.PagingCookie;
                }
                else
                {
                    // If no more records in the result nodes, exit the loop.
                    break;
                }
            }
        }

        public static void QueryStaticCampaignList(string listId, ServiceClient serviceClient)
        {
            var fetchXml =
                $@" <fetch>
                    <entity name=""listmember"">
                        <attribute name=""entitytype"" />
                        <attribute name=""listmemberid"" />
                        <attribute name=""entityid"" />
                        <filter type=""and"">
                            <condition attribute=""listid"" operator=""eq"" value=""{listId}"" />
                        </filter>
                        <link-entity name=""contact"" from=""contactid"" to=""entityid"" alias=""Contact"">
                            <attribute name=""emailaddress1"" />
                            <attribute name=""fullname"" />
                        </link-entity>
                    </entity>
                </fetch>";

            // Define the fetch attributes.
            // Set the number of records per page to retrieve.
            int fetchCount = listPageSize;

            // Initialize the page number.
            int pageNumber = 1;

            // Initialize the number of records.
            int recordCount = 0;

            // Specify the current paging cookie. For retrieving the first page, 
            // pagingCookie should be null.
            string pagingCookie = null;

            while (true)
            {
                // Build fetchXml string with the placeholders.
                string xml = CreatePaginatedXml(fetchXml, pagingCookie, pageNumber, fetchCount);

                // Excute the fetch query and get the xml result.
                RetrieveMultipleRequest fetchRequest = new RetrieveMultipleRequest
                {
                    Query = new FetchExpression(xml)
                };

                EntityCollection returnCollection = ((RetrieveMultipleResponse)serviceClient.Execute(fetchRequest)).EntityCollection;

                List<CampaignContact> campaignContacts = new List<CampaignContact>();
                foreach (var c in returnCollection.Entities)
                {
                    var campaignContact = new CampaignContact()
                    {
                        EmailAddress = ((AliasedValue)c.Attributes["Contact.emailaddress1"]).Value.ToString(),
                        FullName = ((AliasedValue)c.Attributes["Contact.fullname"]).Value.ToString(),
                    };
                    campaignContacts.Add(campaignContact);

                    System.Console.WriteLine("{0}.\t{1}\t\t{2}",
                        ++recordCount,
                        campaignContact.FullName,
                        campaignContact.EmailAddress);

                }
                CampaignMailer.SendMessage(campaignContacts);

                // Check for morerecords, if it returns 1.
                if (returnCollection.MoreRecords)
                {
                    Console.WriteLine("\n****************\nPage number {0}\n****************", pageNumber);

                    // Increment the page number to retrieve the next page.
                    pageNumber++;

                    // Set the paging cookie to the paging cookie returned from current results.                            
                    pagingCookie = returnCollection.PagingCookie;
                }
                else
                {
                    // If no more records in the result nodes, exit the loop.
                    break;
                }
            }
        }

        public static string LookupCampaignListID(string listName, ServiceClient serviceClient)
        {
            // Query a campaign name
            var query = new QueryByAttribute("list")
            {
                ColumnSet = new ColumnSet("listid")
            };
            query.AddAttributeValue("listname", listName);

            var results = serviceClient.RetrieveMultiple(query);
            return results.Entities.First().Attributes["listid"].ToString();
        }

        public static string CreatePaginatedXml(string xml, string cookie, int page, int count)
        {
            StringReader stringReader = new StringReader(xml);
            var reader = new XmlTextReader(stringReader);

            // Load document
            XmlDocument doc = new XmlDocument();
            doc.Load(reader);

            XmlAttributeCollection attrs = doc.DocumentElement.Attributes;

            if (cookie != null)
            {
                XmlAttribute pagingAttr = doc.CreateAttribute("paging-cookie");
                pagingAttr.Value = cookie;
                attrs.Append(pagingAttr);
            }

            XmlAttribute pageAttr = doc.CreateAttribute("page");
            pageAttr.Value = System.Convert.ToString(page);
            attrs.Append(pageAttr);

            XmlAttribute countAttr = doc.CreateAttribute("count");
            countAttr.Value = System.Convert.ToString(count);
            attrs.Append(countAttr);

            StringBuilder sb = new StringBuilder(1024);
            StringWriter stringWriter = new StringWriter(sb);

            XmlTextWriter writer = new XmlTextWriter(stringWriter);
            doc.WriteTo(writer);
            writer.Close();

            return sb.ToString();
        }

        public static string EnsureContactRecordHasEmailAttribute(string xml)
        {
            var xDocument = XDocument.Parse(xml);

            // Find the contact entity node
            var entity = xDocument.Descendants("entity").Where(e => e?.Attribute("name").Value == "contact").First();

            // does an email address attribute exist?
            var emailAttributeExists = entity.Elements("attribute").Where(e => e?.Attribute("name").Value == "emailaddress1").Any();

            // If it doesn't, create one
            if (!emailAttributeExists)
            {
                entity.Add(new XElement("attribute", new XAttribute("name", "emailaddress1")));
            }

            // return the udpated xml
            return xDocument.ToString();
        }
    }
}
