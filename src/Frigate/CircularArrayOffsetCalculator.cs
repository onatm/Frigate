using System;
using System.Threading;

namespace Frigate
{
    internal static class CircularArrayOffsetCalculator
    {
        public static readonly int BufferPad = 64 * 2 / IntPtr.Size;

        /// <summary>Calculates offset in bytes within the array for a given index.</summary>
        /// <param name="index">desired item index</param>
        /// <param name="mask"></param>
        /// <returns>offset of the item</returns>
        public static long CalculateItemOffset(long index, long mask) => BufferPad + (index & mask);

        /// <summary>A volatile load (LoadLoad barrier) of an item from a given offset.</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="buffer"></param>
        /// <param name="offset">offset computed via <see cref="CalculateItemOffset"/></param>
        /// <returns>item at the offset</returns>
        public static T VolatileLoad<T>(T[] buffer, long offset) where T : class => Volatile.Read(ref buffer[offset]);
    }
}
