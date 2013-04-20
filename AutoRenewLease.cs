using Microsoft.WindowsAzure.StorageClient;
using System;
using System.Threading;
using System.Net;
using System.Globalization;

namespace smarx.WazStorageExtensions
{
    public class AutoRenewLease : IDisposable
    {
        public bool HasLease { get { return LeaseId != null; } }

        public string LeaseId { get; private set; }

        private CloudBlob blob;
        private Thread renewalThread;
        private bool disposed = false;

        public static void DoOnce(CloudBlob blob, Action action) { DoOnce(blob, action, TimeSpan.FromSeconds(5)); }
        public static void DoOnce(CloudBlob blob, Action action, TimeSpan pollingFrequency)
        {
            // blob.Exists has the side effect of calling blob.FetchAttributes, which populates the metadata collection
            while (!blob.Exists() || blob.Metadata["progress"] != "done")
            {
                using (var arl = new AutoRenewLease(blob))
                {
                    if (arl.HasLease)
                    {
                        action();
                        blob.Metadata["progress"] = "done";
                        blob.SetMetadata(arl.LeaseId);
                    }
                    else
                    {
                        Thread.Sleep(pollingFrequency);
                    }
                }
            }
        }

        public static void DoEvery(CloudBlob blob, TimeSpan interval, Action action)
        {
            while (true)
            {
                var lastPerformed = DateTimeOffset.MinValue;
                using (var arl = new AutoRenewLease(blob))
                {
                    if (arl.HasLease)
                    {
                        blob.FetchAttributes();
                        DateTimeOffset.TryParseExact(blob.Metadata["lastPerformed"], "R", CultureInfo.CurrentCulture, DateTimeStyles.AdjustToUniversal, out lastPerformed);
                        if (DateTimeOffset.UtcNow >= lastPerformed + interval)
                        {
                            action();
                            lastPerformed = DateTimeOffset.UtcNow;
                            blob.Metadata["lastPerformed"] = lastPerformed.ToString("R");
                            blob.SetMetadata(arl.LeaseId);
                        }
                    }
                }
                var timeLeft = (lastPerformed + interval) - DateTimeOffset.UtcNow;
                var minimum = TimeSpan.FromSeconds(5); // so we're not polling the leased blob too fast
                Thread.Sleep(
                    timeLeft > minimum
                    ? timeLeft
                    : minimum);
            }
        }

        public AutoRenewLease(CloudBlob blob)
        {
            this.blob = blob;
            blob.Container.CreateIfNotExist();
            try
            {
                blob.UploadByteArray(new byte[0], new BlobRequestOptions { AccessCondition = AccessCondition.IfNoneMatch("*") });
            }
            catch (StorageClientException e)
            {
                if (e.ErrorCode != StorageErrorCode.BlobAlreadyExists
                    && e.StatusCode != HttpStatusCode.PreconditionFailed) // 412 from trying to modify a blob that's leased
                {
                    throw;
                }
            }
            LeaseId = blob.TryAcquireLease();
            if (HasLease)
            {
                renewalThread = new Thread(() =>
                {
                    while (true)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(40));
                        blob.RenewLease(LeaseId);
                    }
                });
                renewalThread.Start();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    if (renewalThread != null)
                    {
                        renewalThread.Abort();
                        blob.ReleaseLease(LeaseId);
                        renewalThread = null;
                    }
                }
                disposed = true;
            }
        }

        ~AutoRenewLease()
        {
            Dispose(false);
        }
    }
}