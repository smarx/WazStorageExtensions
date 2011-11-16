using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;
using smarx.WazStorageExtensions;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace WorkerRole1
{
    public class FibonacciCalculator
    {
        private bool isCalculating;

        public void Calculate()
        {
            CloudStorageAccount.SetConfigurationSettingPublisher((configName, configSetter) => {
                configSetter(RoleEnvironment.GetConfigurationSettingValue(configName));
            });

            var acct = CloudStorageAccount.FromConfigurationSetting("StorageConnectionString");
            var client = acct.CreateCloudBlobClient();
            var taskBlob = client.GetBlobReference("dev-task-blobs/fib-task-blob");
            var autoRenew = new AutoRenewLease(taskBlob);

            if (autoRenew.HasLease) {


                var cancellationSource = new CancellationTokenSource();
                var cancellationToken = cancellationSource.Token;
                var calculatorState = new CalculatorState { MaxNumbersInSequence = 48};

                var calculatorTask = Task.Factory.StartNew<CalculatorResult>((state) => {

                    isCalculating = true;
                    cancellationToken.ThrowIfCancellationRequested();

                    var cs = (CalculatorState)state;
                    var calculatorResult = new CalculatorResult();
                    Func<int, int> fib = null;
                    fib = number => number > 1 ? fib(number - 1) + fib(number - 2) : number;

                    while (autoRenew.HasLease && isCalculating && !cancellationToken.IsCancellationRequested) {
                        for (int i = 0; i < cs.MaxNumbersInSequence; i++) {
                            if (!autoRenew.HasLease) {
                                isCalculating = false;
                                Trace.WriteLine("autoRenew.HasLease is false.  Breaking out of calculation routine.");
                                cancellationSource.Cancel();
                                break;
                            }
                            else {
                                var nextNumber = fib(i);
                                calculatorResult.Numbers.Add(nextNumber);
                                Trace.WriteLine(String.Format("ix:{0} - {1}", (i + 1), nextNumber));
                            }
                        }

                        isCalculating = false;
                    }

                    autoRenew.Dispose();
                    cancellationToken.ThrowIfCancellationRequested();
                    return calculatorResult;

                }, calculatorState, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

                calculatorTask.ContinueWith(CompletedAction(autoRenew), cancellationToken, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);

                calculatorTask.ContinueWith(ErrorAction(autoRenew), TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        private Action<Task<CalculatorResult>> ErrorAction(AutoRenewLease autoRenew)
        {
            return (task) => {
                task.Exception.Handle((inner) => {
                    if (inner is OperationCanceledException) {
                        autoRenew.Dispose();
                        Trace.WriteLine("Calculation Canceled");
                    }
                    else {
                        Trace.WriteLine("Unhandled exception when calculating the fibonacci sequence...", UnwindException(inner));
                    }

                    isCalculating = false;
                    return true;
                });
            };
        }

        private Action<Task<CalculatorResult>> CompletedAction(AutoRenewLease autoRenew)
        {
            return (task) => {
                autoRenew.Dispose();
                Trace.WriteLine("Calculation is complete...");
                Trace.WriteLine("Last number in the sequence is: " + task.Result.Numbers.Last());

                isCalculating = false;
            };
        }
        private string UnwindException(Exception ex)
        {
            var current = ex;
            var builder = new StringBuilder();
            while (current != null) {
                builder.Append(current.ToString());
                current = current.InnerException;
            }

            return builder.ToString();
        }
        public bool IsCalculating
        {
            get
            {
                return isCalculating;
            }
        }


        public FibonacciCalculator()
        {
            isCalculating = false;
        }
    }

    public class CalculatorState
    {
        public AutoRenewLease LeaseManager { get; set; }
        public int MaxNumbersInSequence { get; set; }

        public CalculatorState()
        {

        }
    }
    public class CalculatorResult
    {
        public List<int> Numbers { get; set; }

        public CalculatorResult()
        {
            Numbers = new List<int>();
        }
    }
}
