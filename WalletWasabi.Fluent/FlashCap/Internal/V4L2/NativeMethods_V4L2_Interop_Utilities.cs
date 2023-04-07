////////////////////////////////////////////////////////////////////////////
//
// FlashCap - Independent camera capture library.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace FlashCap.Internal.V4L2
{
    partial class NativeMethods_V4L2_Interop
    {
        public virtual uint V4L2_PIX_FMT_YUY2 => 844715353U;
        public virtual uint V4L2_PIX_FMT_ARGB => 1111970369U;
        
        ////////////////////////////////////////////////////////////

        protected static unsafe byte[] get(byte* p, int length)
        {
            var arr = new byte[length];
            fixed (byte* pd = arr)
            {
                NativeMethods.CopyMemory(
                    (IntPtr)pd, (IntPtr)p, (IntPtr)(length * sizeof(byte)));
            }
            return arr;
        }

        protected static unsafe void set(byte* p, byte[] value, int length)
        {
            if (value.Length != length)
            {
                throw new ArgumentException();
            }
            fixed (byte* ps = value)
            {
                NativeMethods.CopyMemory(
                    (IntPtr)p, (IntPtr)ps, (IntPtr)(length * sizeof(byte)));
            }
        }
        
        ////////////////////////////////////////////////////////////

        protected static unsafe ushort[] get(ushort* p, int length)
        {
            var arr = new ushort[length];
            fixed (ushort* pd = arr)
            {
                NativeMethods.CopyMemory(
                    (IntPtr)pd, (IntPtr)p, (IntPtr)(length * sizeof(ushort)));
            }
            return arr;
        }

        protected static unsafe void set(ushort* p, ushort[] value, int length)
        {
            if (value.Length != length)
            {
                throw new ArgumentException();
            }
            fixed (ushort* ps = value)
            {
                NativeMethods.CopyMemory(
                    (IntPtr)p, (IntPtr)ps, (IntPtr)(length * sizeof(ushort)));
            }
        }
        
        ////////////////////////////////////////////////////////////

        protected static unsafe int[] get(int* p, int length)
        {
            var arr = new int[length];
            fixed (int* pd = arr)
            {
                NativeMethods.CopyMemory(
                    (IntPtr)pd, (IntPtr)p, (IntPtr)(length * sizeof(int)));
            }
            return arr;
        }

        protected static unsafe void set(int* p, int[] value, int length)
        {
            if (value.Length != length)
            {
                throw new ArgumentException();
            }
            fixed (int* ps = value)
            {
                NativeMethods.CopyMemory(
                    (IntPtr)p, (IntPtr)ps, (IntPtr)(length * sizeof(int)));
            }
        }
        
        ////////////////////////////////////////////////////////////

        protected static unsafe uint[] get(uint* p, int length)
        {
            var arr = new uint[length];
            fixed (uint* pd = arr)
            {
                NativeMethods.CopyMemory(
                    (IntPtr)pd, (IntPtr)p, (IntPtr)(length * sizeof(uint)));
            }
            return arr;
        }

        protected static unsafe void set(uint* p, uint[] value, int length)
        {
            if (value.Length != length)
            {
                throw new ArgumentException();
            }
            fixed (uint* ps = value)
            {
                NativeMethods.CopyMemory(
                    (IntPtr)p, (IntPtr)ps, (IntPtr)(length * sizeof(uint)));
            }
        }
        
        ////////////////////////////////////////////////////////////

        protected static unsafe ushort[][] get(
            ushort* p, int length0, int length1)
        {
            var arr = new ushort[length0][];
            for (var index = 0; index < arr.Length; index++)
            {
                var iarr = new ushort[length1];
                arr[index] = iarr;
                fixed (ushort* pd = iarr)
                {
                    NativeMethods.CopyMemory(
                        (IntPtr)pd, (IntPtr)p, (IntPtr)(arr.Length * sizeof(ushort)));
                }
            }
            return arr;
        }

        protected static unsafe void set(
            ushort* p, ushort[][] value, int length0, int length1)
        {
            if (value.Length != length0)
            {
                throw new ArgumentException();
            }

            foreach (var iarr in value)
            {
                if (iarr.Length != length1)
                {
                    throw new ArgumentException();
                }
                
                fixed (ushort* ps = iarr)
                {
                    NativeMethods.CopyMemory(
                        (IntPtr)p, (IntPtr)ps, (IntPtr)(length1 * sizeof(ushort)));
                }
            }
        }

        ////////////////////////////////////////////////////////////

        protected static unsafe TI[] get<T, TI>(byte* p, int elementSize, int length)
            where T : struct
            where TI: class
        {
            var arr = new T[length];
            var h = GCHandle.Alloc(arr, GCHandleType.Pinned);
            try
            {
                NativeMethods.CopyMemory(
                    h.AddrOfPinnedObject(), (IntPtr)p, (IntPtr)(length * elementSize));
            }
            finally
            {
                h.Free();
            }
            return arr.Cast<TI>().ToArray();
        }

        protected static unsafe void set<T, TI>(byte* p, TI[] value, int elementSize, int length)
            where T : struct
            where TI: class
        {
            if (value.Length != length)
            {
                throw new ArgumentException();
            }

            var arr = value.Cast<T>().ToArray();
            var h = GCHandle.Alloc(arr, GCHandleType.Pinned);
            try
            {
                NativeMethods.CopyMemory(
                    (IntPtr)p, h.AddrOfPinnedObject(), (IntPtr)(length * elementSize));
            }
            finally
            {
                h.Free();
            }
        }
    }
}
