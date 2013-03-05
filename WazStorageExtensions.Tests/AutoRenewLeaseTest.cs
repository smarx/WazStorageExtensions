using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using smarx.WazStorageExtensions;
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
            //_account = new CloudStorageAccount(new StorageCredentials("hockeyhawk", "Y/CxrkEV8R7tMz4bJWbXvm+paRAxPbHydkzsA2CInvo9BYzL9NLxu2puYgRFL4jonvqucEQVArNGWyok3rYwOw=="), true);
            _client = _account.CreateCloudBlobClient();
        }

        public void Dispose()
        {
            var blob = _client.GetContainerReference("testcontainer").GetBlockBlobReference("testblob");
            blob.Container.DeleteIfExists();
        }

        [Fact]
        public async Task test_can_get_first_lease_async()
        {
            var blob = _client.GetContainerReference("testcontainer")
                .GetBlockBlobReference(Guid.NewGuid().ToString());

            Assert.False(blob.Exists(), "lease blob already exists");

            var arl = await AutoRenewLease.GetAutoRenewLeaseAsync(blob);
            Assert.True(arl.HasLease);

            blob.FetchAttributes();
            Assert.Equal(LeaseState.Leased, blob.Properties.LeaseState);
            Assert.Equal(LeaseStatus.Locked, blob.Properties.LeaseStatus);
        }

        [Fact]
        public async Task test_can_only_get_first_lease_async()
        {
            var blob = _client.GetContainerReference("testcontainer")
                .GetBlockBlobReference(Guid.NewGuid().ToString());

            Assert.False(blob.Exists(), "lease blob already exists");

            var arl1 = await AutoRenewLease.GetAutoRenewLeaseAsync(blob);
            Assert.True(arl1.HasLease);

            blob.FetchAttributes();
            Assert.Equal(LeaseState.Leased, blob.Properties.LeaseState);
            Assert.Equal(LeaseStatus.Locked, blob.Properties.LeaseStatus);

            var arl2 = await AutoRenewLease.GetAutoRenewLeaseAsync(blob);
            Assert.False(arl2.HasLease);
        }

        [Fact]
        public async Task test_try_get_many_leases()
        {
            var blob = _client.GetContainerReference("testcontainer")
                .GetBlockBlobReference(Guid.NewGuid().ToString());

            Assert.False(blob.Exists(), "lease blob already exists");

            var arl1 = await AutoRenewLease.GetAutoRenewLeaseAsync(blob);
            Assert.True(arl1.HasLease);

            blob.FetchAttributes();
            Assert.Equal(LeaseState.Leased, blob.Properties.LeaseState);
            Assert.Equal(LeaseStatus.Locked, blob.Properties.LeaseStatus);

            var array = new Task<AutoRenewLease>[100];

            for (var x = 0; x < array.Length; x++)
            {
                array[x] = AutoRenewLease.GetAutoRenewLeaseAsync(blob);
            }
           
            Task.WaitAll(array);

            foreach (var t in array)
            {
                Assert.False(t.Result.HasLease);
            }
        }


        [Fact]
        public async Task test_dispose_autorenewlease_async()
        {
            var blob = _client.GetContainerReference("testcontainer")
                .GetBlockBlobReference(Guid.NewGuid().ToString());

            Assert.False(blob.Exists(), "lease blob already exists");

            using (var arl1 = await AutoRenewLease.GetAutoRenewLeaseAsync(blob))
            {
                Assert.True(arl1.HasLease);
                blob.FetchAttributes();
                Assert.Equal(LeaseState.Leased, blob.Properties.LeaseState);
                Assert.Equal(LeaseStatus.Locked, blob.Properties.LeaseStatus);

                var arl2 = await AutoRenewLease.GetAutoRenewLeaseAsync(blob);
                Assert.False(arl2.HasLease);
            }

            blob.FetchAttributes();
            Assert.Equal(LeaseStatus.Unlocked, blob.Properties.LeaseStatus);

            var arl3 = await AutoRenewLease.GetAutoRenewLeaseAsync(blob);
            Assert.True(arl3.HasLease);

            blob.FetchAttributes(); 
            Assert.Equal(LeaseState.Leased, blob.Properties.LeaseState);
            Assert.Equal(LeaseStatus.Locked, blob.Properties.LeaseStatus);
        }

        [Fact]
        public async Task test_doonce()
        {
            var blob = _client.GetContainerReference("testcontainer")
                .GetBlockBlobReference(Guid.NewGuid().ToString());

            Assert.False(blob.Exists(), "lease blob already exists");

            int i = 0;

            await AutoRenewLease.DoOnceAsync(blob, () => i++, TimeSpan.FromSeconds(1));

            foreach (var _ in Enumerable.Range(1,100))
            {
                await AutoRenewLease.DoOnceAsync(blob, () => i++, TimeSpan.FromSeconds(1));
            }
            
            Assert.Equal(1, i);
            Assert.Equal("done", blob.Metadata["progress"]);
        }

        [Fact]
        public void test_doonce_exception()
        {
            var blob = _client.GetContainerReference("testcontainer")
                .GetBlockBlobReference(Guid.NewGuid().ToString());

            Assert.False(blob.Exists(), "lease blob already exists");

            var t = AutoRenewLease.DoOnceAsync(blob, () => { throw new ApplicationException("failure"); }, TimeSpan.FromSeconds(1000));

            Assert.Throws<AggregateException>(() => t.Wait());
        }
    }
}
