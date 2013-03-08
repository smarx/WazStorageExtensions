using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using DevHawk.WazStorageExtensions;
using System.Threading.Tasks;

namespace WazStorageExtensions.Tests
{
    public class BlobExtensionTests : IDisposable
    {
        CloudStorageAccount _account = CloudStorageAccount.DevelopmentStorageAccount;

        CloudBlobClient _client;
        CloudBlobContainer _container; 

        public BlobExtensionTests()
        {
            _client = _account.CreateCloudBlobClient();
            _container = _client
                .GetContainerReference("testcontainer");

            _container.CreateIfNotExists();
        }

        public void Dispose()
        {
            _container.DeleteIfExists();
        }

        [Fact]
        public async Task test_try_aquire_lease()
        {
            var blob = _container
                .GetBlockBlobReference(Guid.NewGuid().ToString());

            using (var ms = new System.IO.MemoryStream(new byte[0]))
            {
                blob.UploadFromStream(ms);
            }

            var lid = await blob.TryAquireLeaseAsync();
            var lid2 = await blob.TryAquireLeaseAsync();

            Assert.NotNull(lid);
            Assert.Null(lid2);
        }

        [Fact]
        public async Task test_try_aquire_lease_throws_with_missing_blob()
        {
            var blob = _container
                .GetBlockBlobReference(Guid.NewGuid().ToString());

            Assert.False(blob.Exists());

            var ex = await RecordExceptionAsync(async () => await blob.TryAquireLeaseAsync());
            Assert.NotNull(ex);
            Assert.IsType<StorageException>(ex);
        }

        public static async Task<Exception> RecordExceptionAsync(Func<Task> code)
        {
            try
            {
                await code();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }


    }
}
