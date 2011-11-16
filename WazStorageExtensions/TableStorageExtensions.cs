using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.StorageClient;
using System.Net;

namespace smarx.WazStorageExtensions
{
    public static class TableStorageExtensions
    {
        public static TableServiceContext GetDataServiceContext2011(this CloudTableClient client)
        {
            var ctx = client.GetDataServiceContext();
            ctx.SendingRequest += (_, e) => e.RequestHeaders["x-ms-version"] = "2011-08-18";
            return ctx;
        }
    }
}
