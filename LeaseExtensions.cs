using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.IO;
using System.Threading;
using System.Net;
using System.Threading.Tasks;

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

        public static async Task<string> TryAquireLeaseAsync(this ICloudBlob blob, TimeSpan? leaseTime = null, string proposedLeaseId = null, AccessCondition accessCondition = null, BlobRequestOptions options = null, OperationContext operationContext = null)
        {
            try
            {
                return await Task.Factory.FromAsync<string>(
                    (cb, ob) => blob.BeginAcquireLease(leaseTime, proposedLeaseId, accessCondition, options, operationContext, cb, ob),
                    blob.EndAcquireLease,
                    null);
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation.HttpStatusCode == (int)HttpStatusCode.Conflict)
                    return null;
                else
                    throw;
            }
        }
    }
}