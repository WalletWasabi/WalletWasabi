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
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawNewPath")]
        public static extern IntPtr DrawNewPath(FillMode fillMode);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawFreePath")]
        public static extern void DrawFreePath(IntPtr path);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawPathNewFigure")]
        public static extern void DrawPathNewFigure(IntPtr path, double x, double y);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawPathNewFigureWithArc")]
        public static extern void DrawPathNewFigureWithArc(IntPtr path, double xCenter, double yCenter, double radius, double startAngle, double sweep, bool negative);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawPathLineTo")]
        public static extern void DrawPathLineTo(IntPtr path, double x, double y);

        // notes: angles are both relative to 0 and go counterclockwise
        // TODO is the initial line segment on cairo and OS X a proper join?
        // TODO what if sweep < 0?
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawPathArcTo")]
        public static extern void DrawPathArcTo(IntPtr path, double xCenter, double yCenter, double radius, double startAngle, double sweep, bool negative);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawPathBezierTo")]
        public static extern void DrawPathBezierTo(IntPtr path, double c1x, double c1y, double c2x, double c2y, double endX, double endY);
        
        // TODO quadratic bezier
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawPathCloseFigure")]
        public static extern void DrawPathCloseFigure(IntPtr path);

        // TODO effect of these when a figure is already started
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawPathAddRectangle")]
        public static extern void DrawPathAddRectangle(IntPtr path, double x, double y, double width, double height);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawPathEnd")]
        public static extern void DrawPathEnd(IntPtr path);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawStroke")]
        public static extern void DrawStroke(IntPtr context, IntPtr path, ref BrushInternal brush, ref StrokeParamsInternal strokeParam);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawFill")]
        public static extern void DrawFill(IntPtr context, IntPtr path, ref BrushInternal brush);
    }
}
