﻿using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.IO;
using System.Threading;
using System.Net;
using System.Threading.Tasks;

namespace DevHawk.WazStorageExtensions
{
    public static class BlobExtensions
    {
        public static string TryAcquireLease(this ICloudBlob blob, TimeSpan? leaseTime = null, string proposedLeaseId = null, AccessCondition accessCondition = null, BlobRequestOptions options = null, OperationContext operationContext = null)
        {
            try { return blob.AcquireLease(leaseTime, proposedLeaseId, accessCondition, options, operationContext); }
            catch (StorageException ex)
            {
                if (ex.RequestInformation.HttpStatusCode == (int)HttpStatusCode.Conflict)
                    return null;
                else
                    throw;
            }
        }

        public static Task SetMetadataAsync(this ICloudBlob blob, AccessCondition accessCondition = null, BlobRequestOptions options = null, OperationContext operationContext = null)
        {
            return Task.Factory.FromAsync(
                (cb, ob) => blob.BeginSetMetadata(accessCondition, options, operationContext, cb, ob),
                blob.EndSetMetadata,
                null);
        }

        public static Task FetchAttributesAsync(this ICloudBlob blob, AccessCondition accessCondition = null, BlobRequestOptions options = null, OperationContext operationContext = null)
        {
            return Task.Factory.FromAsync(
                (cb, ob) => blob.BeginFetchAttributes(accessCondition, options, operationContext, cb, ob),
                blob.EndFetchAttributes,
                null);
        }

        public static bool TryRenewLease(this ICloudBlob blob, AccessCondition accessCondition, BlobRequestOptions options = null, OperationContext operationContext = null)
        {
            try { blob.RenewLease(accessCondition, options, operationContext); return true; }
            catch { return false; }
        }

        public static Task<string> AquireLeaseAsync(this ICloudBlob blob, TimeSpan? leaseTime = null, string proposedLeaseId = null, AccessCondition accessCondition = null, BlobRequestOptions options = null, OperationContext operationContext = null)
        {
            return Task.Factory.FromAsync<string>(
                    (cb, ob) => blob.BeginAcquireLease(leaseTime, proposedLeaseId, accessCondition, options, operationContext, cb, ob),
                    blob.EndAcquireLease,
                    null);
        }

        public static Task<string> TryAquireLeaseAsync(this ICloudBlob blob, TimeSpan? leaseTime = null, string proposedLeaseId = null, AccessCondition accessCondition = null, BlobRequestOptions options = null, OperationContext operationContext = null)
        {
            var tcs = new TaskCompletionSource<string>();

            blob.AquireLeaseAsync(leaseTime, proposedLeaseId, accessCondition, options, operationContext)
                .ContinueWith(t =>
                {
                    if (!t.IsFaulted)
                    {
                        tcs.SetResult(t.Result);
                    }
                    else
                    {
                        if (t.Exception.InnerExceptions.Count > 1)
                            tcs.TrySetException(t.Exception);
                        else
                        {
                            var ex = t.Exception.InnerExceptions[0] as StorageException;

                            if (ex != null && ex.RequestInformation.HttpStatusCode == (int)HttpStatusCode.Conflict)
                            {
                                tcs.SetResult(null);
                            }
                            else
                            {
                                tcs.TrySetException(t.Exception.InnerExceptions[0]);
                            }
                        }
                    }
                });

            return tcs.Task;
        }

        public static Task<bool> CreateIfNotExistsAsync(this CloudBlobContainer container)
        {
            return Task.Factory.FromAsync<bool>(
                    (cb, ob) => container.BeginCreateIfNotExists(cb, ob),
                    container.EndCreateIfNotExists,
                    null);
        }

        public static Task UploadFromStreamAsync(this ICloudBlob blockBlob, System.IO.Stream stream, AccessCondition accessCondition = null, BlobRequestOptions options = null, OperationContext operationContext = null)
        {
            return Task.Factory.FromAsync(
                (cb, ob) => blockBlob.BeginUploadFromStream(stream, accessCondition, options, operationContext, cb, ob),
                blockBlob.EndUploadFromStream,
                null);
        }

        public static Task DownloadToStreamAsync(this ICloudBlob blob, System.IO.Stream stream, AccessCondition accessCondition = null, BlobRequestOptions options = null, OperationContext operationContext = null)
        {
            return Task.Factory.FromAsync(
                (cb, ob) => blob.BeginDownloadToStream(stream, accessCondition, options, operationContext, cb, ob),
                blob.EndDownloadToStream,
                null);
        }

        public static Task<bool> ExistsAsync(this ICloudBlob blob, BlobRequestOptions options = null, OperationContext operationContext = null)
        {
            return Task.Factory.FromAsync<bool>(
                (cb, ob) => blob.BeginExists(options, operationContext, cb, ob),
                blob.EndExists,
                null);
        }
    }
}