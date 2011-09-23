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

This console app performs an upsert and a server-side query projection:

    public class MyEntity : TableServiceEntity
    {
        public string Value { get; set; }
        public DateTime Created { get; set; }
        public MyEntity() { }
        public MyEntity(string key, string value) : base(string.Empty, key) { Value = value; Created = DateTime.UtcNow; }
    }
	...
    static void Main(string[] args)
    {
        var account = CloudStorageAccount.Parse(args[0]);
        account.Ensure(tables: new[] { "temptable" });
        var ctx = account.CreateCloudTableClient().GetDataServiceContext2011();
        var entity = new MyEntity(args[1], args[2]);
        ctx.AttachTo("temptable", entity, null);
        ctx.UpdateObject(entity);
		 // Does "InsertOrReplace." Drop the SaveChangesOptions argument to get "InsertOrMerge."
        ctx.SaveChangesWithRetries(SaveChangesOptions.ReplaceOnUpdate);

        // Server-side projection! "Created" property is not returned.
        Console.WriteLine(
		    (from e in account.CreateCloudTableClient().GetDataServiceContext2011().CreateQuery<MyEntity>("temptable")
             where e.PartitionKey == string.Empty && e.RowKey == args[1]
             select new { e.Value }).Single().Value);
    }

This console app exercises queue message updates:

    static void Main(string[] args)
    {
        var account = CloudStorageAccount.Parse(args[0]);
        account.Ensure(queues: new[] { "tempqueue" });

        var q = account.CreateCloudQueueClient().GetQueueReference("tempqueue");
        q.Clear();

        Console.WriteLine("Adding message that will appear in 5 seconds...");
        q.AddMessageDelayed(new CloudQueueMessage("hello, world"), TimeSpan.FromSeconds(5));

        if (q.GetMessage() == null) Console.WriteLine("Received nothing, since message is still hidden.");

        Console.WriteLine("Sleeping for 6 seconds...");
        Thread.Sleep(TimeSpan.FromSeconds(6));

        CloudQueueMessageMutable msg = q.GetMessageMutable(TimeSpan.FromSeconds(5));
        Console.WriteLine("Got message: " + msg.AsString);
        Console.WriteLine("Next visible: " + msg.NextVisibleTime.Value.ToUniversalTime());

        Console.WriteLine("Updating contents...");
        q.UpdateMessage(msg, "goodbye, world");
        Console.WriteLine("Now contains: " + msg.AsString);

        Console.WriteLine("Setting next visible time to now...");
        q.RenewMessage(msg, TimeSpan.FromSeconds(0));
        Console.WriteLine("Next visible: " + msg.NextVisibleTime.Value.ToUniversalTime());

        Console.WriteLine("Sleeping for 1 second just to be sure...");
        Thread.Sleep(TimeSpan.FromSeconds(1));

        CloudQueueMessage msg2 = q.GetMessage();
        Console.WriteLine("Received message: " + msg2.AsString);
        q.DeleteMessage(msg2);

        Console.WriteLine("Message deleted.");
        Console.WriteLine("Done.");
    }