using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;

namespace smarx.WazStorageExtensions
{
    public static class InitializationExtensions
    {
        public static void Ensure(this CloudStorageAccount account, IEnumerable<string> tables = null, IEnumerable<string> containers = null, IEnumerable<string> queues = null)
        {
            var tableClient = account.CreateCloudTableClient();
            var blobClient = account.CreateCloudBlobClient();
            var queueClient = account.CreateCloudQueueClient();

            if (tables != null) foreach (var table in tables) tableClient.CreateTableIfNotExist(table);
            if (containers != null) foreach (var container in containers) blobClient.GetContainerReference(container).CreateIfNotExist();
            if (queues != null) foreach (var queue in queues) queueClient.GetQueueReference(queue).CreateIfNotExist();
        }
    }
}
