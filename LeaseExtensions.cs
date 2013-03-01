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


        //// NOTE: This method doesn't do everything that the regular UploadText does.
        //// Notably, it doesn't update the BlobProperties of the blob (with the new
        //// ETag and LastModifiedTimeUtc). It also, like all the methods in this file,
        //// doesn't apply any retry logic. Use this at your own risk!
        //public static void UploadText(this CloudBlob blob, string text, string leaseId)
        //{
        //    string url = blob.Uri.AbsoluteUri;
        //    if (blob.ServiceClient.Credentials.NeedsTransformUri)
        //    {
        //        url = blob.ServiceClient.Credentials.TransformUri(url);
        //    }
        //    var req = BlobRequest.Put(new Uri(blob.ServiceClient.Credentials.TransformUri(blob.Uri.AbsoluteUri)),
        //        90, new BlobProperties(), BlobType.BlockBlob, leaseId, 0);
        //    using (var writer = new StreamWriter(req.GetRequestStream()))
        //    {
        //        writer.Write(text);
        //    }
        //    blob.ServiceClient.Credentials.SignRequest(req);
        //    req.GetResponse().Close();
        //}

        //public static void SetMetadata(this CloudBlob blob, string leaseId)
        //{
        //    var req = BlobRequest.SetMetadata(new Uri(blob.ServiceClient.Credentials.TransformUri(blob.Uri.AbsoluteUri)), 90, leaseId);
        //    foreach (string key in blob.Metadata.Keys)
        //    {
        //        req.Headers.Add("x-ms-meta-" + key, blob.Metadata[key]);
        //    }
        //    blob.ServiceClient.Credentials.SignRequest(req);
        //    req.GetResponse().Close();
        //}
    }
}