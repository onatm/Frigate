using System.Runtime.InteropServices;
using System.Threading;
using Frigate.Utilities;

namespace Frigate
{
    /// <summary>
    /// <para>
    /// A Multi-Producer-Single-Consumer queue.
    /// </para>
    /// <para>
    /// This implementation follows patterns for False Sharing protection and the Fast Flow method for polling from the queue and
    /// an extension of the Leslie Lamport concurrent queue algorithm (originated by Martin Thompson) on the producer side.
    /// </para>
    /// <para>
    /// Load/Store methods using a buffer parameter are provided to allow the prevention of final field reload after a LoadLoad
    /// barrier.
    /// </para>
    /// </summary>
    public class MpscQueue<T> where T : class
    {
        private PaddedHeadAndTail paddedHeadAndTail;

        private PaddedMaskAndCapacity maskAndCapacity;

        private readonly T[] circularBuffer;

        private long HeadIndex
        {
            get => Volatile.Read(ref paddedHeadAndTail.Head);

            set => Volatile.Write(ref paddedHeadAndTail.Head, value);
        }

        private long TailIndex => Volatile.Read(ref paddedHeadAndTail.Tail);

        private bool TrySetTailIndex(long expect, long newValue) =>
            Interlocked.CompareExchange(ref paddedHeadAndTail.Tail, newValue, expect) == expect;

        private int Capacity => maskAndCapacity.Capacity;

        public bool IsEmpty => HeadIndex == TailIndex;

        public int Count
        {
            get
            {
                var after = HeadIndex;
                while (true)
                {
                    var before = after;
                    var currentTailIndex = TailIndex;
                    after = HeadIndex;
                    if (before == after)
                    {
                        return (int)(currentTailIndex - after);
                    }
                }
            }
        }

        public MpscQueue(int capacity)
        {
            maskAndCapacity.Capacity = IntegerExtensions.RoundToPowerOfTwo(capacity);
            maskAndCapacity.Mask = maskAndCapacity.Capacity - 1;
            circularBuffer = new T[Capacity + CircularArrayOffsetCalculator.BufferPad * 2];
        }

        /// <summary>Lock free enqueue using a single CAS. As class name suggests access is permitted to many threads concurrently.</summary>
        /// <param name="item">item to be enqueued</param>
        /// <returns><code>true</code>if the item was added to this queue, else <code>false</code></returns>
        public bool TryEnqueue(T item)
        {
            var spinner = new SpinWait();

            while (true)
            {
                var currentHeadIndex = HeadIndex;

                var currentTailIndex = TailIndex;

                var limitIndex = currentTailIndex - Capacity;

                if (currentHeadIndex <= limitIndex)
                {
                    return false;
                }

                if (TrySetTailIndex(currentTailIndex, currentTailIndex + 1))
                {
                    var offset = CircularArrayOffsetCalculator.CalculateItemOffset(currentTailIndex, maskAndCapacity.Mask);
                    Volatile.Write(ref circularBuffer[offset], item);
                    return true;
                }

                spinner.SpinOnce();
            }
        }

        /// <summary> Lock free dequeue using ordered load.</summary>
        /// <param name="item">item to be dequeued</param>
        /// <returns><code>false</code> if queue is empty</returns>
        public bool TryDequeue(out T item)
        {
            var spinner = new SpinWait();

            while (true)
            {
                var currentHeadIndex = HeadIndex;

                var offset = CircularArrayOffsetCalculator.CalculateItemOffset(currentHeadIndex, maskAndCapacity.Mask);

                var tempItem = Volatile.Read(ref circularBuffer[offset]);

                if (tempItem != null)
                {
                    circularBuffer[offset] = default(T);
                    HeadIndex = currentHeadIndex + 1;
                    item = tempItem;
                    return true;
                }

                if (currentHeadIndex == TailIndex)
                {
                    item = default(T);
                    return false;
                }

                spinner.SpinOnce();
            }
        }

        /// <summary>Lock free peek using ordered loads.</summary>
        /// <param name="item">item to be peeked</param>
        /// <returns><code>false</code> if queue is empty</returns>
        public bool TryPeek(out T item)
        {
            var buffer = circularBuffer;

            var headIndex = HeadIndex; // LoadLoad
            var offset = CircularArrayOffsetCalculator.CalculateItemOffset(headIndex, maskAndCapacity.Mask);
            var tempItem = CircularArrayOffsetCalculator.VolatileLoad(buffer, offset);
            if (tempItem == null)
            {
                if (headIndex != TailIndex)
                {
                    do
                    {
                        tempItem = CircularArrayOffsetCalculator.VolatileLoad(buffer, offset);
                    }
                    while (tempItem == null);
                }
                else
                {
                    item = default(T);
                    return false;
                }
            }

            item = tempItem;
            return true;
        }

        /// <summary>Dequeue all the things _o/</summary>
        public void Clear()
        {
#pragma warning disable 168
            // ReSharper disable once UnusedVariable
            while (TryDequeue(out var item) || !IsEmpty)
            {
            }
#pragma warning restore 168
        }
    }

    // padding before/between/after fields based on typical cache line size of 64
    [StructLayout(LayoutKind.Explicit, Size = 192)]
    internal struct PaddedHeadAndTail
    {
        [FieldOffset(64)]
        public long Head;

        [FieldOffset(128)]
        public long Tail;
    }

    [StructLayout(LayoutKind.Explicit, Size = 192)]
    internal struct PaddedMaskAndCapacity
    {
        [FieldOffset(64)]
        public int Capacity;

        [FieldOffset(128)]
        public long Mask;
    }
}
