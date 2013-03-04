
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Globalization;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace smarx.WazStorageExtensions
{
    public class AutoRenewLease : IDisposable
    {
        public bool HasLease { get { return leaseId != null; } }

        private ICloudBlob blob;
        private string leaseId;
        private IDisposable subscription;
        private bool disposed = false;

        //TODO: write async versions of DoOnce & DoEvery
        //public static void DoOnce(ICloudBlob blob, Action action) { DoOnce(blob, action, TimeSpan.FromSeconds(5)); }
        //public static void DoOnce(ICloudBlob blob, Action action, TimeSpan pollingFrequency)
        //{
        //    // blob.Exists has the side effect of calling blob.FetchAttributes, which populates the metadata collection
        //    while (!blob.Exists() || blob.Metadata["progress"] != "done")
        //    {
        //        var t = AutoRenewLease.GetAutoRenewLeaseAsync(blob);
        //        t.Wait();

        //        using (var arl = t.Result)
        //        {
        //            if (arl.HasLease)
        //            {
        //                action();
        //                blob.Metadata["progress"] = "done";
        //                blob.SetMetadata(AccessCondition.GenerateLeaseCondition(arl.leaseId));
        //            }
        //            else
        //            {
        //                Thread.Sleep(pollingFrequency);
        //            }
        //        }
        //    }
        //}

        //public static void DoEvery(ICloudBlob blob, TimeSpan interval, Action action)
        //{
        //    while (true)
        //    {
        //        var lastPerformed = DateTimeOffset.MinValue;
        //        var t = AutoRenewLease.GetAutoRenewLeaseAsync(blob);
        //        t.Wait();

        //        using (var arl = t.Result)
        //        {
        //            if (arl.HasLease)
        //            {
        //                blob.FetchAttributes();
        //                DateTimeOffset.TryParseExact(blob.Metadata["lastPerformed"], "R", CultureInfo.CurrentCulture, DateTimeStyles.AdjustToUniversal, out lastPerformed);
        //                if (DateTimeOffset.UtcNow >= lastPerformed + interval)
        //                {
        //                    action();
        //                    lastPerformed = DateTimeOffset.UtcNow;
        //                    blob.Metadata["lastPerformed"] = lastPerformed.ToString("R");
        //                    blob.SetMetadata(AccessCondition.GenerateLeaseCondition(arl.leaseId));
        //                }
        //            }
        //        }
        //        var timeLeft = (lastPerformed + interval) - DateTimeOffset.UtcNow;
        //        var minimum = TimeSpan.FromSeconds(5); // so we're not polling the leased blob too fast
        //        Thread.Sleep(
        //            timeLeft > minimum
        //            ? timeLeft
        //            : minimum);
        //    }
        //}

        private AutoRenewLease(ICloudBlob blob, string leaseId, IDisposable subscription)
        {
            this.blob = blob;
            this.leaseId = leaseId;
            this.subscription = subscription;
        }

        public static async Task<AutoRenewLease> GetAutoRenewLeaseAsync(ICloudBlob blob)
        {
            await Task.Factory.FromAsync<bool>(
                (cb, ob) => blob.Container.BeginCreateIfNotExists(cb, ob),
                blob.Container.EndCreateIfNotExists,
                null);

            try
            {
                using (var ms = new System.IO.MemoryStream(new byte[0]))
                {
                    await Task.Factory.FromAsync(
                        (cb,ob) => blob.BeginUploadFromStream(ms, AccessCondition.GenerateIfNoneMatchCondition("*"), null, null, cb, ob),
                        blob.EndUploadFromStream,
                        null);
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

            var leaseId = await blob.TryAquireLeaseAsync(TimeSpan.FromSeconds(60));
            IDisposable subscription = null;

            if (leaseId != null)
            {
                subscription = System.Reactive.Linq.Observable.Interval(TimeSpan.FromSeconds(40))
                    .Subscribe(
                        _ => blob.ReleaseLease(AccessCondition.GenerateLeaseCondition(leaseId))
                    );
            }

            return new AutoRenewLease(blob, leaseId, subscription);
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
                    if (subscription != null)
                    {
                        subscription.Dispose();
                        blob.ReleaseLease(AccessCondition.GenerateLeaseCondition(leaseId));
                        subscription = null;
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