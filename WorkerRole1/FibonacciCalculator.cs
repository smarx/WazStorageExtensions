using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;

namespace WorkerRole1
{
    public class FibonacciCalculator
    {
        private bool isCalculating;

        public void Calculate()
        {

            var cancellationSource = new CancellationTokenSource();
            var cancellationToken = cancellationSource.Token;
            var calculatorState = new CalculatorState { MaxNumbersInSequence = 70 };

            var calculatorTask = Task.Factory.StartNew<CalculatorResult>((state) => {

                isCalculating = true;
                cancellationToken.ThrowIfCancellationRequested();

                var cs = (CalculatorState)state;
                var calculatorResult = new CalculatorResult();
                Func<int, int> fib = null;

                while (! cancellationToken.IsCancellationRequested) {
                    fib = number => number > 1 ? fib(number - 1) + fib(number - 2) : number;

                    // fetch the first 100 numbers in the fibonacci sequence
                    for (int i = 0; i < cs.MaxNumbersInSequence; i++) {
                        var nextNumber = fib(i);
                        calculatorResult.Numbers.Add(nextNumber);
                        Trace.WriteLine(String.Format("ix:{0} - {1}", (i + 1), nextNumber));
                    }
                }

                isCalculating = false;
                return calculatorResult;

            }, calculatorState, cancellationToken,  TaskCreationOptions.LongRunning, TaskScheduler.Default);

            calculatorTask.ContinueWith(CompletedAction(), cancellationToken, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);

            calculatorTask.ContinueWith(ErrorAction(), TaskContinuationOptions.OnlyOnFaulted);
        }

        private Action<Task<CalculatorResult>> ErrorAction()
        {
            return (task) => {
                task.Exception.Handle((inner) => {
                    if (inner is OperationCanceledException) {
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

        private Action<Task<CalculatorResult>> CompletedAction()
        {
            return (task) => {
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
        public int MaxNumbersInSequence { get; set; }

        public CalculatorState()
        {
            
        }
    }
    public class CalculatorResult
    {
        public List<int> Numbers {get; set;}

        public CalculatorResult()
        {
            Numbers = new List<int>();
        }
    }
}
