////////////////////////////////////////////////////////////////////////////
//
// FlashCap - Independent camera capture library.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using FlashCap.Internal;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

#if NET35 || NET40 || NET45
namespace System
{
    internal static class ArrayEx
    {
        private static class EmptyArray<T>
        {
            public static readonly T[] Empty = new T[0];
        }

#if NET45
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static T[] Empty<T>() =>
            EmptyArray<T>.Empty;
    }
}
#else
namespace System
{
    internal static class ArrayEx
    {
#if NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static T[] Empty<T>() =>
            Array.Empty<T>();
    }
}
#endif

namespace System.Linq
{
    internal static partial class EnumerableExtension
    {
        public static IEnumerable<U> Collect<T, U>(
            this IEnumerable<T> enumerable, Func<T, U?> selector)
        {
            foreach (var value in enumerable)
            {
                if (selector(value) is { } mapped)
                {
                    yield return mapped;
                }
            }
        }
        
        public static IEnumerable<U> CollectWhile<T, U>(
            this IEnumerable<T> enumerable, Func<T, U?> selector)
        {
            foreach (var value in enumerable)
            {
                if (selector(value) is { } mapped)
                {
                    yield return mapped;
                }
                else
                {
                    break;
                }
            }
        }
    }
}

#if NETSTANDARD1_3
namespace System.Security
{
    // HACK: dummy
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method)]
    internal sealed class SuppressUnmanagedCodeSecurityAttribute : Attribute
    {
    }
}

namespace System.Diagnostics
{
    internal static class Trace
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteLine(object? obj) =>
            Debug.WriteLine(obj);
    }
}

namespace System.Threading
{
    internal enum ApartmentState
    {
        STA,
        MTA,
        Unknown,
    }

    internal delegate void ThreadStart();

    internal sealed class Thread
    {
        private readonly ThreadStart entryPoint;
        private ApartmentState state = ApartmentState.Unknown;
        private Tasks.Task? task;

        public Thread(ThreadStart entryPoint) =>
            this.entryPoint = entryPoint;

        public bool IsBackground { get; set; }

        public void SetApartmentState(ApartmentState state) =>
            this.state = state;

        private void EntryPoint()
        {
            if (NativeMethods.CurrentPlatform == NativeMethods.Platforms.Windows)
            {
                switch (this.state)
                {
                    case ApartmentState.STA:
                        NativeMethods.CoUninitialize();   // DIRTY
                        NativeMethods.CoInitializeEx(
                            IntPtr.Zero, NativeMethods.COINIT.APARTMENTTHREADED);
                        break;
                    case ApartmentState.MTA:
                        NativeMethods.CoUninitialize();   // DIRTY
                        NativeMethods.CoInitializeEx(
                            IntPtr.Zero, NativeMethods.COINIT.MULTITHREADED);
                        break;
                }
            }
            try
            {
                this.entryPoint();
            }
            finally
            {
                NativeMethods.CoUninitialize();   // DIRTY
            }
        }

        public void Start() =>
            this.task = Tasks.Task.Factory.StartNew(
                this.EntryPoint,
                Tasks.TaskCreationOptions.LongRunning);

        public void Join() =>
            this.task?.Wait();

        public void Join(TimeSpan timeout) =>
            this.task?.Wait(timeout);
    }

    internal delegate void WaitCallback(object? parameter);

    internal static class ThreadPool
    {
        public static bool QueueUserWorkItem(WaitCallback workItem, object? parameter)
        {
            Tasks.Task.Factory.StartNew(p => workItem(p), parameter);
            return true;
        }
    }
}
#endif

#if !(NET35 || NET40)
namespace System.Threading.Tasks
{
    internal static class TaskCompat
    {
#if NET45
        public static Task CompletedTask =>
            Task.FromResult(0);
#else
        public static Task CompletedTask =>
            Task.CompletedTask;
#endif

#if NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static Task<Task> WhenAny(params Task[] tasks) =>
            Task.WhenAny(tasks);

#if NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static Task<T> FromResult<T>(T value) =>
            Task.FromResult(value);
    }
}
#endif

#if NET35 || NET40
namespace System.Threading.Tasks
{
    internal static class TaskCompat
    {
        public static Task CompletedTask =>
            TaskEx.FromResult(true);
        public static Task<T> FromResult<T>(T value) =>
            TaskEx.FromResult(value);

        public static Task<Task> WhenAny(params Task[] tasks) =>
            TaskEx.WhenAny(tasks);
    }
}

namespace System.Runtime.ExceptionServices
{
    internal sealed class ExceptionDispatchInfo
    {
        private readonly Exception ex;
        private readonly StackTrace stackTrace;

        private ExceptionDispatchInfo(Exception ex)
        {
            this.ex = ex;
            this.stackTrace = new StackTrace(ex);
        }

        public void Throw() =>
            throw this.ex;     // IGNORED: Will lost stack information.

        public static ExceptionDispatchInfo Capture(Exception ex) =>
            new ExceptionDispatchInfo(ex);
    }
}
#endif

#if NETSTANDARD1_3
namespace System.Threading.Tasks
{
    internal static class Parallel
    {
        public static void For(int fromInclusive, int toExclusive, Action<int> body)
        {
            using var waiter = new ManualResetEventSlim(false);
            var running = 1;

            var trampoline = new WaitCallback(parameter =>
            {
                try
                {
                    body((int)parameter!);
                }
                finally
                {
                    if (Interlocked.Decrement(ref running) <= 0)
                    {
                        waiter.Set();
                    }
                }
            });

            for (var index = fromInclusive; index < toExclusive; index++)
            {
                Interlocked.Increment(ref running);
                ThreadPool.QueueUserWorkItem(trampoline, index);
            }

            if (Interlocked.Decrement(ref running) >= 1)
            {
                waiter.Wait();
            }
        }
    }
}
#endif
