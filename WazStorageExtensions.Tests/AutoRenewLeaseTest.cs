using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace WazStorageExtensions.Tests
{

    public class AutoRenewLeaseTest : IDisposable
    {
        CloudStorageAccount _account = CloudStorageAccount.DevelopmentStorageAccount;

        CloudBlobClient _client;

        public AutoRenewLeaseTest()
        {
            _client = _account.CreateCloudBlobClient();

            var blob = _client.GetContainerReference("testcontainer").GetBlockBlobReference("testblob");

            blob.DeleteIfExists();
            blob.Container.DeleteIfExists();
        }

        public void Dispose()
        {
            var blob = _client.GetContainerReference("testcontainer").GetBlockBlobReference("testblob");

            blob.FetchAttributes();
            if (blob.Properties.LeaseState != LeaseState.Available)
                blob.BreakLease(TimeSpan.Zero);
            blob.DeleteIfExists();
            blob.Container.DeleteIfExists();
        }

        [Fact]
        public async Task test_can_get_first_lease_async()
        {
            var blob = _client.GetContainerReference("testcontainer").GetBlockBlobReference("testblob");

            Assert.False(blob.Container.Exists());
            Assert.False(blob.Exists());

            var arl = await smarx.WazStorageExtensions.AutoRenewLease.GetAutoRenewLeaseAsync(blob);
            Assert.True(arl.HasLease);

            blob.FetchAttributes();
            Assert.Equal(LeaseState.Leased, blob.Properties.LeaseState);
            Assert.Equal(LeaseStatus.Locked, blob.Properties.LeaseStatus);
        }

        [Fact]
        public async Task test_can_only_get_first_lease_async()
        {
            var blob = _client.GetContainerReference("testcontainer").GetBlockBlobReference("testblob");

            Assert.False(blob.Container.Exists()); 
            Assert.False(blob.Exists());

            var arl1 = await smarx.WazStorageExtensions.AutoRenewLease.GetAutoRenewLeaseAsync(blob);
            Assert.True(arl1.HasLease);

            blob.FetchAttributes();
            Assert.Equal(LeaseState.Leased, blob.Properties.LeaseState);
            Assert.Equal(LeaseStatus.Locked, blob.Properties.LeaseStatus);

            var arl2 = await smarx.WazStorageExtensions.AutoRenewLease.GetAutoRenewLeaseAsync(blob);
            Assert.False(arl2.HasLease);
        }

        [Fact]
        public async Task test_dispose_autorenewlease_async()
        {
            var blob = _client.GetContainerReference("testcontainer").GetBlockBlobReference("testblob");

            Assert.False(blob.Container.Exists()); 
            Assert.False(blob.Exists());

            using (var arl1 = await smarx.WazStorageExtensions.AutoRenewLease.GetAutoRenewLeaseAsync(blob))
            {
                Assert.True(arl1.HasLease);
                blob.FetchAttributes();
                Assert.Equal(LeaseState.Leased, blob.Properties.LeaseState);
                Assert.Equal(LeaseStatus.Locked, blob.Properties.LeaseStatus);

                var arl2 = await smarx.WazStorageExtensions.AutoRenewLease.GetAutoRenewLeaseAsync(blob);
                Assert.False(arl2.HasLease);
            }

            blob.FetchAttributes();
            Assert.Equal(LeaseStatus.Unlocked, blob.Properties.LeaseStatus);

            var arl3 = await smarx.WazStorageExtensions.AutoRenewLease.GetAutoRenewLeaseAsync(blob);
            Assert.True(arl3.HasLease);

            blob.FetchAttributes(); 
            Assert.Equal(LeaseState.Leased, blob.Properties.LeaseState);
            Assert.Equal(LeaseStatus.Locked, blob.Properties.LeaseStatus);
        }

        [Fact]
        public async Task test_doone()
        {
            var blob = _client.GetContainerReference("testcontainer").GetBlockBlobReference("testblob");

            Assert.False(blob.Container.Exists());
            Assert.False(blob.Exists());
            
            int i = 0;

            await smarx.WazStorageExtensions.AutoRenewLease.DoOnceAsync(blob, () => i++, TimeSpan.FromSeconds(1));
            await smarx.WazStorageExtensions.AutoRenewLease.DoOnceAsync(blob, () => i++, TimeSpan.FromSeconds(1));
            await smarx.WazStorageExtensions.AutoRenewLease.DoOnceAsync(blob, () => i++, TimeSpan.FromSeconds(1));
            await smarx.WazStorageExtensions.AutoRenewLease.DoOnceAsync(blob, () => i++, TimeSpan.FromSeconds(1));

            Assert.Equal(1, i);
            Assert.Equal("done", blob.Metadata["progress"]);

            
        }
    }
}
