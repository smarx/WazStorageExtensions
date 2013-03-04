
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Globalization;
using System.Net;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
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

        public static Task DoOnceAsync(ICloudBlob blob, Action action) { return DoOnceAsync(blob, action, TimeSpan.FromSeconds(5)); }
        public static async Task DoOnceAsync(ICloudBlob blob, Action action, TimeSpan pollingFrequency)
        {
            await Observable.Timer(TimeSpan.FromSeconds(0), pollingFrequency)
                .TakeWhile(_ => !blob.Exists() || blob.Metadata["progress"] != "done")
                .Do(async _ =>
                {
                    using (var arl = await AutoRenewLease.GetAutoRenewLeaseAsync(blob))
                    {
                        if (arl.HasLease)
                        {
                            action();
                            blob.Metadata["progress"] = "done";
                            await blob.SetMetadataAsync(AccessCondition.GenerateLeaseCondition(arl.leaseId));
                        }
                    }
                })
                //need to concat an extra value on the end to ensure there's at least 
                //one value for ToTask to return
                .Concat(Observable.Return<long>(-1))
                .ToTask();
        }

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
            await blob.Container.CreateIfNotExistsAsync();

            try
            {
                using (var ms = new System.IO.MemoryStream(new byte[0]))
                {
                    await blob.UploadFromStreamAsync(ms, AccessCondition.GenerateIfNoneMatchCondition("*"));
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
                subscription = Observable.Interval(TimeSpan.FromSeconds(40))
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