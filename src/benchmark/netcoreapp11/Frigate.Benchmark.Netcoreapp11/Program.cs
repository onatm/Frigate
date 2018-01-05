using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Attributes.Jobs;
using BenchmarkDotNet.Running;

namespace Frigate.Benchmark.Netcoreapp11
{
    public class Program
    {
        static void Main()
        {
            var summary = BenchmarkRunner.Run<MpscQueueVsConcurrentQueue>();
        }

        [DisassemblyDiagnoser(printAsm: true, printSource: true)]
        [RyuJitX64Job]
        public class MpscQueueVsConcurrentQueue
        {
            private const int CAPACITY = 320000;

            private static readonly int DOP = Environment.ProcessorCount * 4;

            private static readonly int ITEMS_PER_PRODUCER = CAPACITY / DOP;

            [Benchmark]
            public void MpscQueue()
            {
                var tasks = new List<Task>();

                var mpscArrayQueue = new MpscQueue<RandomItem>(CAPACITY);

                var remainingItems = CAPACITY;

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

                for (var i = 0; i < DOP; i++)
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
            }

            [Benchmark]
            public void ConcurrentQueue()
            {
                var tasks = new List<Task>();

                var concurrentQueue = new ConcurrentQueue<RandomItem>();

                var remainingItems = CAPACITY;

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

                for (var i = 0; i < DOP; i++)
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
            }
        }
    }

    public class RandomItem
    {
        public int Value { get; }

        public RandomItem(int value)
        {
            Value = value;
        }
    }
}
