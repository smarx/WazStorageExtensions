using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;

namespace WorkerRole1
{
    public class WorkerRole : RoleEntryPoint
    {
        private Random random = new Random(DateTime.Now.Millisecond);

        public override void Run()
        {
            // This is a sample worker implementation. Replace with your logic.
            Trace.WriteLine("WorkerRole1 entry point called", "Information");

            var fibonacciCalculator = new FibonacciCalculator();
            while (true) {
                Thread.Sleep(10000);

                if (! fibonacciCalculator.IsCalculating) {
                    
                    var randomSleepInterval = random.Next(5, 30);
                    Trace.TraceInformation("Sleeping for '{0}'", randomSleepInterval);
                    Thread.Sleep(TimeSpan.FromSeconds(randomSleepInterval));

                    fibonacciCalculator.Calculate();
                }

                Trace.WriteLine("Working", "Information");
            }
        }

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

            return base.OnStart();
        }
    }
}
