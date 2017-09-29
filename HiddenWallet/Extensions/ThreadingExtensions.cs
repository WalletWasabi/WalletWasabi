using System;
using System.Collections.Generic;
using System.Text;

namespace System.Threading
{
    public static class ThreadingExtensions
    {
        public static void SafeRelease(this SemaphoreSlim me)
        {
            try
            {
                me.Release();
            }
            catch(SemaphoreFullException)
            {
                // ignore SemaphoreSlim brainfart
            }
        }
    }
}
