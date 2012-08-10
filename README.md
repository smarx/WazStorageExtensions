WazStorageExtensions
====================

smarx.WazStorageExtensions is a collection of useful extension methods for Windows Azure storage operations that aren't covered by the .NET client library.

It can be install using the [NuGet package](http://nuget.org/List/Packages/smarx.WazStorageExtensions) via `install-package smarx.WazStorageExtensions` and contains extension methods and classes for the following:

* Storage Analytics API ([MSDN documentation](http://msdn.microsoft.com/en-us/library/hh343270.aspx))
* Working with blob leases ([blog post](http://blog.smarx.com/posts/leasing-windows-azure-blobs-using-the-storage-client-library))
* Testing existence of blobs and containers ([blog post](http://blog.smarx.com/posts/testing-existence-of-a-windows-azure-blob))
* Updating queue message visibility timeouts and content ([storage team blog post](http://blogs.msdn.com/b/windowsazurestorage/archive/2011/09/15/windows-azure-queues-improved-leases-progress-tracking-and-scheduling-of-future-work.aspx))
* Upsert and server-side query projection ([storage team blog post](http://blogs.msdn.com/b/windowsazurestorage/archive/2011/09/15/windows-azure-tables-introducing-upsert-and-query-projection.aspx))
* A convenience method to initialize storage by creating containers, tables, and queues in a single call

Basic Usage
-----------

This console app initializes storage by creating a container, queue, and table:

    public static void Main(string[] args)
    {
        var account = CloudStorageAccount.Parse(args[0]);
        account.Ensure(containers: new [] { "mycontainer" }, queues: new [] { "myqueue" }, tables: new [] { "mytable" });
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
