
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


        public static Task DoOnceAsync(ICloudBlob blob, Action action) { return DoOnceAsync(blob, action, TimeSpan.FromSeconds(5)); }
        public static async Task DoOnceAsync(ICloudBlob blob, Action action, TimeSpan pollingFrequency)
        {
            var tcs = new TaskCompletionSource<int>();

            TimerCallback timer_action = async _ =>
            {
                try
                {
                    if ((await blob.ExistsAsync()) && blob.Metadata["progress"] == "done")
                        tcs.SetResult(0);

                    using (var arl = await AutoRenewLease.GetAutoRenewLeaseAsync(blob))
                    {
                        if (arl.HasLease)
                        {
                            action();
                            blob.Metadata["progress"] = "done";
                            await blob.SetMetadataAsync(AccessCondition.GenerateLeaseCondition(arl.leaseId));
                        }
                    }
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            };

            using (var timer = new Timer(timer_action, null, TimeSpan.FromSeconds(0), pollingFrequency))
            {
                await tcs.Task;
            }
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
                subscription = new Timer(
                    _ => blob.RenewLease(AccessCondition.GenerateLeaseCondition(leaseId)), 
                    null, 
                    TimeSpan.FromSeconds(40), 
                    TimeSpan.FromSeconds(40));
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