
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Globalization;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DevHawk.WazStorageExtensions
{
    public interface IAutoRenewLease : IDisposable
    {
        bool HasLease { get; }
        string LeaseId { get; }
    }

    public class AutoRenewLease : IAutoRenewLease, IDisposable
    {
        public bool HasLease { get { return leaseId != null; } }
        public string LeaseId { get { return leaseId; } }

        private ICloudBlob blob;
        private string leaseId;
        private IDisposable subscription;
        private bool disposed = false;


        public static Task DoOnceAsync(ICloudBlob blob, Action action) { return DoOnceAsync(blob, action, TimeSpan.FromSeconds(5)); }
        public static async Task DoOnceAsync(ICloudBlob blob, Action action, TimeSpan pollingFrequency)
        {
            var tcs = new TaskCompletionSource<object>();

            TimerCallback timer_action = async _ =>
            {
                try
                {
                    if ((await blob.ExistsAsync()) && blob.Metadata["progress"] == "done")
                    {
                        tcs.SetResult(null);
                        return;
                    }

                    using (var arl = await AutoRenewLease.GetAutoRenewLeaseAsync(blob))
                    {
                        if (arl.HasLease)
                        {
                            if (!blob.Metadata.ContainsKey("progress") || blob.Metadata["progress"] != "done")
                            {
                                action();
                                blob.Metadata["progress"] = "done";
                                await blob.SetMetadataAsync(AccessCondition.GenerateLeaseCondition(arl.leaseId));
                            }
                            else
                            {
                                tcs.SetResult(null);
                                return;
                            }
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

        class TimerState
        {
            public Timer Timer;
        }

        //Note, This version of DoEvery does not block like the original version
        //the returned IDisposable can be used to cancel the recurring operation
        public static IDisposable DoEvery(ICloudBlob blob, Action action, TimeSpan interval)
        {
            TimerCallback timer_proc = async state =>
            {
                var timerState = (TimerState)state;

                var lastPerformed = (await blob.ExistsAsync()) && blob.Metadata.ContainsKey("lastPerformed")
                    ? DateTimeOffset.ParseExact(blob.Metadata["lastPerformed"], "R", CultureInfo.CurrentCulture, DateTimeStyles.AdjustToUniversal)
                    : DateTimeOffset.MinValue;

                if (DateTimeOffset.UtcNow >= lastPerformed + interval)
                {
                    using (var arl = await AutoRenewLease.GetAutoRenewLeaseAsync(blob))
                    {
                        if (arl.HasLease)
                        {
                            action();

                            lastPerformed = DateTimeOffset.UtcNow;
                            blob.Metadata["lastPerformed"] = lastPerformed.ToString("R");
                            await blob.SetMetadataAsync(AccessCondition.GenerateLeaseCondition(arl.leaseId));
                        }
                    }
                }

                var timeLeft = (lastPerformed + interval) - DateTimeOffset.UtcNow;
                timeLeft = timeLeft < TimeSpan.Zero ? TimeSpan.Zero : timeLeft;

                timerState.Timer.Change(timeLeft, interval);
            };

            var ts = new TimerState();
            ts.Timer = new Timer(timer_proc, ts, TimeSpan.FromSeconds(1), interval);

            return ts.Timer;
        }

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