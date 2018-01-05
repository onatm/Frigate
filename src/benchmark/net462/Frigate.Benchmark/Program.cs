using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable UnusedVariable
namespace Frigate.Benchmark
{
    class Program
    {
        private const int RUN_COUNT = 6;
        
        private const int CAPACITY = 3200000;

        private static readonly int DOP = Environment.ProcessorCount * 4;

        private static readonly int ITEMS_PER_PRODUCER = CAPACITY / DOP;
        
        // ReSharper disable once UnusedMember.Local
        static void Main()
        {
            Console.WriteLine($"Producing {CAPACITY} items with {DOP} producers. Cosuming with 1 consumer.");
            Console.WriteLine($"Will run for {RUN_COUNT} times.");

            var concurrentQueueAverage = ConcurrentQueueAverage(CAPACITY, DOP, RUN_COUNT);

            Console.WriteLine($"ConcurrentQueue average: {concurrentQueueAverage}ms");

            var mpscQueueAverage = MpscQueueAverage(CAPACITY, DOP, RUN_COUNT);

            Console.WriteLine($"MpscArrayQueue average: {mpscQueueAverage}ms");
        }

        private static double ConcurrentQueueAverage(int capacity, int dop, int runCount)
        {
            var concurrentQueueAverage =
                Enumerable.Range(0, runCount).Select(_ =>
                {
                    var tasks = new List<Task>();

                    var concurrentQueue = new ConcurrentQueue<RandomItem>();

                    var sw = new Stopwatch();
                    sw.Start();

                    var remainingItems = capacity;

                    tasks.Add(Task.Run(() =>
                    {
                        while (Volatile.Read(ref remainingItems) > 0)
                        {
                            if (concurrentQueue.TryDequeue(out RandomItem item))
                            {
                                Interlocked.Decrement(ref remainingItems);
                            }
                            else
                            {
                                Task.Delay(1);
                            }
                        }
                    }));

                    for (var i = 0; i < dop; i++)
                    {
                        tasks.Add(Task.Run(() =>
                        {
                            for (var j = 0; j < ITEMS_PER_PRODUCER; j++)
                            {
                                concurrentQueue.Enqueue(new RandomItem(j));
                            }
                        }));
                    }

                    Task.WaitAll(tasks.ToArray());

                    return sw.ElapsedMilliseconds;
                }).Skip(1).Average();
            return concurrentQueueAverage;
        }

        private static double MpscQueueAverage(int capacity, int dop, int runCount)
        {
            var mpscArrayQueueAverage =
                Enumerable.Range(0, runCount).Select(_ =>
                {
                    var tasks = new List<Task>();

                    var mpscArrayQueue = new MpscQueue<RandomItem>(capacity);

                    var sw = new Stopwatch();
                    sw.Start();

                    var remainingItems = capacity;

                    tasks.Add(Task.Run(() =>
                    {
                        while (Volatile.Read(ref remainingItems) > 0)
                        {
                            if (mpscArrayQueue.TryDequeue(out RandomItem item))
                            {
                                Interlocked.Decrement(ref remainingItems);
                            }
                            else
                            {
                                Task.Delay(1);
                            }
                        }
                    }));

                    for (var i = 0; i < dop; i++)
                    {
                        tasks.Add(Task.Run(() =>
                        {
                            for (var j = 0; j < ITEMS_PER_PRODUCER; j++)
                            {
                                mpscArrayQueue.TryEnqueue(new RandomItem(j));
                            }
                        }));
                    }

                    Task.WaitAll(tasks.ToArray());

                    return sw.ElapsedMilliseconds;
                }).Skip(1).Average();
            return mpscArrayQueueAverage;
        }
    }

    public class RandomItem
    {
        // ReSharper disable once MemberCanBePrivate.Global
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public int Value { get; }

        public RandomItem(int value)
        {
            Value = value;
        }
    }
}
