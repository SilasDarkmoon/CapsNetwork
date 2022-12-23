using System;

namespace Capstones.Net
{
    public class UnsafeMemMove
    {
        public static void MemMove(IntPtr src, IntPtr dst, int count)
        {
            unsafe
            {
                Buffer.MemoryCopy((void*)src, (void*)dst, count, count);
            }
        }
    }
}
