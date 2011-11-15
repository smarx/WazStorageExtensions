using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.StorageClient;
using System.Net;
using Microsoft.WindowsAzure.StorageClient.Protocol;

namespace smarx.WazStorageExtensions
{
    public class CloudQueueMessageMutable : CloudQueueMessage
    {
        public new string Id { get; set; }
        public new string PopReceipt { get; set; }
        public new DateTime? NextVisibleTime { get; set; }
        public new DateTime? ExpirationTime { get; set; }
        public new DateTime? InsertionTime { get; set; }
        public string Content { private get; set; }
        public new string AsString { get { return Encoding.UTF8.GetString(this.AsBytes); } }
        public new byte[] AsBytes { get { return Convert.FromBase64String(this.Content); } }
        public CloudQueueMessageMutable(byte[] content) : base(content) { }
        public CloudQueueMessageMutable(string content) : base(content) { }
        public CloudQueueMessageMutable(QueueMessage message) : base(Convert.FromBase64String(message.Text))
        {
            this.Id = message.Id;
            this.PopReceipt = message.PopReceipt;
            this.NextVisibleTime = message.TimeNextVisible;
            this.ExpirationTime = message.ExpirationTime;
            this.InsertionTime = message.InsertionTime;
            this.Content = message.Text;
        }
    }

    public static class QueueExtensions
    {
        public static void UpdateMessage(this CloudQueue queue, CloudQueueMessageMutable message, byte[] body)
        {
            UpdateMessage(queue, message, Convert.ToBase64String(body), null);
        }
        public static void UpdateMessage(this CloudQueue queue, CloudQueueMessageMutable message, string body)
        {
            UpdateMessage(queue, message, Convert.ToBase64String(Encoding.UTF8.GetBytes(body)), null);
        }
        public static void RenewMessage(this CloudQueue queue, CloudQueueMessageMutable message, TimeSpan visibilityTimeout)
        {
            UpdateMessage(queue, message, null, visibilityTimeout);
        }
        public static void UpdateMessage(this CloudQueue queue, CloudQueueMessageMutable message, string body, TimeSpan? visibilityTimeout)
        {
            var req = (HttpWebRequest)WebRequest.Create(
                queue.Uri.AbsoluteUri +
                string.Format("/messages/{0}?popreceipt={1}&visibilitytimeout={2}",
                    message.Id, Uri.EscapeDataString(message.PopReceipt),
                    (int)(visibilityTimeout.HasValue
                     ? visibilityTimeout.Value
                     : (message.NextVisibleTime.Value - DateTime.UtcNow)
                    ).TotalSeconds)
            );
            req.Method = "PUT";
            req.Headers["x-ms-version"] = "2011-08-18";
            if (body != null)
            {
                var bytes = QueueRequest.GenerateMessageRequestBody(body);
                req.ContentLength = bytes.Length;
                queue.ServiceClient.Credentials.SignRequest(req);
                using (var stream = req.GetRequestStream())
                {
                    stream.Write(bytes, 0, bytes.Length);
                }
                message.Content = body;
            }
            else
            {
                req.ContentLength = 0;
                queue.ServiceClient.Credentials.SignRequest(req);
            }
            using (var response = (HttpWebResponse)req.GetResponse())
            {
                if (response.StatusCode != HttpStatusCode.NoContent)
                {
                    throw new InvalidOperationException("Unexpected response code: " + response.StatusCode);
                }
                message.PopReceipt = response.Headers["x-ms-popreceipt"];
                message.NextVisibleTime = DateTime.Parse(response.Headers["x-ms-time-next-visible"]);
            }
        }

        public static CloudQueueMessageMutable GetMessageMutable(this CloudQueue queue)
        {
            return GetMessageMutable(queue, null);
        }
        public static CloudQueueMessageMutable GetMessageMutable(this CloudQueue queue, TimeSpan? visibilityTimeout)
        {
            return GetMessagesMutable(queue, 1, visibilityTimeout).FirstOrDefault();
        }
        public static IEnumerable<CloudQueueMessageMutable> GetMessagesMutable(this CloudQueue queue, int messageCount)
        {
            return GetMessagesMutable(queue, messageCount, null);
        }
        public static IEnumerable<CloudQueueMessageMutable> GetMessagesMutable(this CloudQueue queue, int messageCount, TimeSpan? visibilityTimeout)
        {
            var uri = queue.Uri.AbsoluteUri + "/messages?numofmessages=" + messageCount;
            if (visibilityTimeout.HasValue) uri += "&visibilitytimeout=" + (int)visibilityTimeout.Value.TotalSeconds;
            var req = (HttpWebRequest)WebRequest.Create(uri);
            req.Headers["x-ms-version"] = "2011-08-18";
            queue.ServiceClient.Credentials.SignRequest(req);
            using (var response = (HttpWebResponse)req.GetResponse())
            {
                return QueueResponse.GetMessages(response).Messages.Select(m => new CloudQueueMessageMutable(m)).ToArray();
            }
        }

        public static void DeleteMessageMutable(this CloudQueue queue, CloudQueueMessageMutable message)
        {
            queue.DeleteMessage(message.Id, message.PopReceipt);
        }

        public static void AddMessageDelayed(this CloudQueue queue, CloudQueueMessage message, TimeSpan visibilityTimeout)
        {
            var req = (HttpWebRequest)WebRequest.Create(queue.Uri.AbsoluteUri + "/messages?visibilitytimeout=" + (int)visibilityTimeout.TotalSeconds);
            req.Method = "POST";
            req.Headers["x-ms-version"] = "2011-08-18";
            var bytes = QueueRequest.GenerateMessageRequestBody(Convert.ToBase64String(message.AsBytes));
            req.ContentLength = bytes.Length;
            queue.ServiceClient.Credentials.SignRequest(req);
            using (var stream = req.GetRequestStream())
            {
                stream.Write(bytes, 0, bytes.Length);
            }
            using (var response = (HttpWebResponse)req.GetResponse())
            {
                if (response.StatusCode != HttpStatusCode.Created)
                {
                    throw new InvalidOperationException("Unexpected response code: " + response.StatusCode);
                }
            }
        }
    }
}