WazStorageExtensions
====================

smarx.WazStorageExtensions is a collection of useful extension methods for Windows Azure storage operations that aren't covered by the .NET client library.

It can be install using the [NuGet package](http://nuget.org/List/Packages/smarx.WazStorageExtensions) via `install-package smarx.WazStorageExtensions` and contains extension methods and classes for the following:

* Storage Analytics API ([MSDN documentation](http://msdn.microsoft.com/en-us/library/hh343270.aspx))
* Working with blob leases ([blog post](http://blog.smarx.com/posts/leasing-windows-azure-blobs-using-the-storage-client-library))
* Testing existence of blobs and containers ([blog post](http://blog.smarx.com/posts/testing-existence-of-a-windows-azure-blob))

Basic Usage
-----------

This console app takes a storage connection string as an argument and enables metrics collection with a 7-day retention policy via the [Storage Analytics API](http://msdn.microsoft.com/en-us/library/hh343270.aspx):

    static void Main(string[] args)
    {
        var blobs = CloudStorageAccount.Parse(args[0]).CreateCloudBlobClient();
        var props = blobs.GetServiceProperties();
        props.Metrics.Enabled = true;
        props.Metrics.RetentionPolicy = new RetentionPolicy { Enabled = true, Days = 7 };
        blobs.SetServiceProperties(props);
    }

This console app tries to acquire a lease on a blob, and (if it succeeds), writes "Hello World" in the blob:

    static void Main(string[] args)
    {
        var blob = CloudStorageAccount.Parse(args[0]).CreateCloudBlobClient().GetBlobReference(args[1]);
        var leaseId = blob.TryAcquireLease();
        if (leaseId != null)
        {
            blob.UploadText("Hello, World!", leaseId);
            blob.ReleaseLease(leaseId);
            Console.WriteLine("Blob written!");
        }
        else
        {
            Console.WriteLine("Blob could not be leased.");
        }
    }

This console app tests for the existence of a blob:

    static void Main(string[] args)
    {
        var blob = CloudStorageAccount.Parse(args[0]).CreateCloudBlobClient().GetBlobReference(args[1]);
        Console.WriteLine("The blob {0}.", blob.Exists() ? "exists" : "doesn't exist");
    }