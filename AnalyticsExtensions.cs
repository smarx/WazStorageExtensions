using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.StorageClient;
using System.Xml.Serialization;
using System.Net;
using System.IO;
using Microsoft.WindowsAzure;

namespace smarx.WazStorageExtensions
{
    public class RetentionPolicy
    {
        public bool Enabled { get; set; }
        public int Days { get; set; }
        public bool DaysSpecified { get { return Enabled; } }
    }
    public class LoggingSettings
    {
        public string Version { get; set; }
        public bool Delete { get; set; }
        public bool Write { get; set; }
        public bool Read { get; set; }
        public RetentionPolicy RetentionPolicy { get; set; }
    }
    public class MetricsSettings
    {
        public string Version { get; set; }
        public bool Enabled { get; set; }
        public bool IncludeAPIs { get; set; }
        public bool IncludeAPIsSpecified { get { return Enabled; } }
        public RetentionPolicy RetentionPolicy { get; set; }
    }
    public class StorageServiceProperties
    {
        public LoggingSettings Logging { get; set; }
        public MetricsSettings Metrics { get; set; }
    }

    public static class AnalyticsExtensions
    {
        private static HttpWebRequest CreateServicePropertiesRequest(Uri baseUri)
        {
            var req = (HttpWebRequest)WebRequest.Create(new UriBuilder(baseUri) { Query = "restype=service&comp=properties" }.Uri);
            req.Headers["x-ms-version"] = "2009-09-19";
            return req;
        }

        private static Uri GetBaseUriForQueues(CloudQueueClient client)
        {
            // ugly way to find the base URI for the queue service, but there's no .BaseUri on a CloudQueueClient
            return new Uri(client.GetQueueReference("foo").Uri.AbsoluteUri.Replace("/foo", string.Empty));
        }

        public static void SetServiceProperties(Uri baseUri, StorageCredentials creds, StorageServiceProperties properties, bool useSharedKeyLite)
        {
            var req = CreateServicePropertiesRequest(baseUri);
            req.Method = "PUT";
            var ms = new MemoryStream();
            new XmlSerializer(typeof(StorageServiceProperties)).Serialize(ms, properties);
            ms.Position = 0;
            req.ContentLength = ms.Length;
            if (useSharedKeyLite) creds.SignRequestLite(req);
            else creds.SignRequest(req);
            using (var stream = req.GetRequestStream())
            {
                ms.CopyTo(stream);
            }
            using (var response = (HttpWebResponse)req.GetResponse())
            {
                if (response.StatusCode != HttpStatusCode.Accepted)
                {
                    throw new Exception("Request failed to return 202 status code.");
                }
            }
        }

        public static StorageServiceProperties GetServiceProperties(Uri baseUri, StorageCredentials creds, bool useSharedKeyLite)
        {
            var req = CreateServicePropertiesRequest(baseUri);
            if (useSharedKeyLite) creds.SignRequestLite(req);
            else creds.SignRequest(req);
            using (var response = req.GetResponse())
            using (var stream = response.GetResponseStream())
            {
                return (StorageServiceProperties)new XmlSerializer(typeof(StorageServiceProperties)).Deserialize(stream);
            }
        }

        public static StorageServiceProperties GetServiceProperties(this CloudBlobClient client)
        {
            return GetServiceProperties(client.BaseUri, client.Credentials, false);
        }
        public static CloudBlobClient SetServiceProperties(this CloudBlobClient client, StorageServiceProperties properties)
        {
            SetServiceProperties(client.BaseUri, client.Credentials, properties, false);
            return client;
        }

        public static StorageServiceProperties GetServiceProperties(this CloudQueueClient client)
        {
            return GetServiceProperties(GetBaseUriForQueues(client), client.Credentials, false);
        }
        public static CloudQueueClient SetServiceProperties(this CloudQueueClient client, StorageServiceProperties properties)
        {
            SetServiceProperties(GetBaseUriForQueues(client), client.Credentials, properties, false);
            return client;
        }

        public static StorageServiceProperties GetServiceProperties(this CloudTableClient client)
        {
            return GetServiceProperties(client.BaseUri, client.Credentials, true);
        }
        public static CloudTableClient SetServiceProperties(this CloudTableClient client, StorageServiceProperties properties)
        {
            SetServiceProperties(client.BaseUri, client.Credentials, properties, true);
            return client;
        }
    }
}