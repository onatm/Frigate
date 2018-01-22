namespace Frigate.Utilities
{
    public class IntegerExtensions
    {
        public static int RoundUpToPowerOfTwo(int v)
        {
            if (v <= 2)
            {
                return 2;
            }

            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v++;
            return v;
        }
    }
}
