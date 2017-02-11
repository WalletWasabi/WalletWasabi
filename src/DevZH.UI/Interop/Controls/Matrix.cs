using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DevZH.UI.Drawing;

namespace DevZH.UI.Interop
{
    internal partial class NativeMethods
    {
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawMatrixSetIdentity")]
        public static extern void DrawMatrixSetIdentity(Matrix matrix);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawMatrixTranslate")]
        public static extern void DrawMatrixTranslate( Matrix matrix, double x, double y);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawMatrixScale")]
        public static extern void DrawMatrixScale( Matrix matrix, double xCenter, double yCenter, double x, double y);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawMatrixRotate")]
        public static extern void DrawMatrixRotate( Matrix matrix, double x, double y, double amount);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawMatrixSkew")]
        public static extern void DrawMatrixSkew( Matrix matrix, double x, double y, double xamount, double yamount);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawMatrixMultiply")]
        public static extern void DrawMatrixMultiply( Matrix dest,  Matrix src);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawMatrixInvertible")]
        public static extern int DrawMatrixInvertible( Matrix matrix);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawMatrixInvert")]
        public static extern int DrawMatrixInvert( Matrix matrix);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawMatrixTransformPoint")]
        public static extern void DrawMatrixTransformPoint( Matrix matrix, out double x, out double y);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawMatrixTransformSize")]
        public static extern void DrawMatrixTransformSize( Matrix matrix, out double x, out double y);


        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawTransform")]
        public static extern void DrawTransform(IntPtr context, Matrix matrix);

        // TODO add a uiDrawPathStrokeToFill() or something like that
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawClip")]
        public static extern void DrawClip(IntPtr context, IntPtr path);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawSave")]
        public static extern void DrawSave(IntPtr context);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawRestore")]
        public static extern void DrawRestore(IntPtr context);
    }
}
