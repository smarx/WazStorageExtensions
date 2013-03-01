
using System;
using System.Threading;
using System.Net;
using System.Globalization;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage;

namespace smarx.WazStorageExtensions
{
    public class AutoRenewLease : IDisposable
    {
        public bool HasLease { get { return leaseId != null; } }

        private ICloudBlob blob;
        private string leaseId;
        private Thread renewalThread;
        private bool disposed = false;

        public static void DoOnce(ICloudBlob blob, Action action) { DoOnce(blob, action, TimeSpan.FromSeconds(5)); }
        public static void DoOnce(ICloudBlob blob, Action action, TimeSpan pollingFrequency)
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
                        blob.SetMetadata(AccessCondition.GenerateLeaseCondition(arl.leaseId));
                    }
                    else
                    {
                        Thread.Sleep(pollingFrequency);
                    }
                }
            }
        }

        public static void DoEvery(ICloudBlob blob, TimeSpan interval, Action action)
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
                            blob.SetMetadata(AccessCondition.GenerateLeaseCondition(arl.leaseId));
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

        public AutoRenewLease(ICloudBlob blob)
        {
            this.blob = blob;
            blob.Container.CreateIfNotExists();
            try
            {
                using (var ms = new System.IO.MemoryStream(new byte[0]))
                {
                    blob.UploadFromStream(ms, AccessCondition.GenerateIfNoneMatchCondition("*"));
                }
            }
            catch (StorageException e)
            {
                if (e.RequestInformation.HttpStatusCode != (int)HttpStatusCode.Conflict &&         // 409 from trying to modify a blob that already exists
                    e.RequestInformation.HttpStatusCode != (int)HttpStatusCode.PreconditionFailed) // 412 from trying to modify a blob that's leased
                {
                    throw;
                }
            }
            leaseId = blob.TryAcquireLease();
            if (HasLease)
            {
                renewalThread = new Thread(() =>
                {
                    while (true)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(40));
                        blob.RenewLease(AccessCondition.GenerateLeaseCondition(leaseId));
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
                        blob.ReleaseLease(AccessCondition.GenerateLeaseCondition(leaseId));
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