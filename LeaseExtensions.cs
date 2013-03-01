using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.IO;
using System.Threading;
using System.Net;

namespace smarx.WazStorageExtensions
{
    public static class LeaseBlobExtensions
    {
        public static string TryAcquireLease(this ICloudBlob blob)
        {
            try { return blob.AcquireLease(null, null); }
            catch (StorageException ex)
            {
                if (ex.RequestInformation.HttpStatusCode == (int)HttpStatusCode.Conflict)
                    return null;
                else
                    throw;
            }
        }

        public static bool TryRenewLease(this ICloudBlob blob, string leaseId)
        {
            try { blob.RenewLease(AccessCondition.GenerateLeaseCondition(leaseId)); return true; }
            catch { return false; }
        }
    }
}