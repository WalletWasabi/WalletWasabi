using System;
using System.Collections.Generic;
using System.Text;

namespace System.Threading.Tasks
{
    public static class TasksExtensions
    {
        public static Task WithCancellation(this Task me, CancellationToken cancel)
        {
            return me.ContinueWith(async t => await t, cancel);
        }

        public static void AwaiterWait(this Task me)
        {
            me.GetAwaiter().GetResult();
        }

        public static T AwaiterResult<T>(this Task<T> me)
        {
            return me.GetAwaiter().GetResult();
        }
    }
}
