using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI.Interop;

namespace DevZH.UI.Drawing
{
    public class Path : Shape
    {
        public Path(FillMode mode)
        {
            this.ControlHandle = NativeMethods.DrawNewPath(mode);
        }

        public void AddRectangle(double x, double y, double width, double height)
        {
            NativeMethods.DrawPathAddRectangle(ControlHandle, x, y, width, height);
        }

        public void End()
        {
            NativeMethods.DrawPathEnd(ControlHandle);
        }

        public void Free()
        {
            NativeMethods.DrawFreePath(ControlHandle);
        }

        public void NewFigure(double x, double y)
        {
            NativeMethods.DrawPathNewFigure(ControlHandle, x, y);
        }

        public void NewFigureWithArc(double xCenter, double yCenter, double radius, double startAngle, double sweep, bool negative)
        {
            NativeMethods.DrawPathNewFigureWithArc(ControlHandle, xCenter, yCenter, radius, startAngle, sweep, negative);
        }

        public void CloseFigure()
        {
            NativeMethods.DrawPathCloseFigure(ControlHandle);
        }

        public void LineTo(double x, double y)
        {
            NativeMethods.DrawPathLineTo(ControlHandle, x, y);
        }

        public void ArcTo(double xCenter, double yCenter, double radius, double startAngle, double sweep, bool negative)
        {
            NativeMethods.DrawPathArcTo(ControlHandle, xCenter, yCenter, radius, startAngle, sweep, negative);
        }

        public void BezierTo(double c1x, double c1y, double c2x, double c2y, double endX, double endY)
        {
            NativeMethods.DrawPathBezierTo(ControlHandle, c1x, c1y, c2x, c2y, endX, endY);
        }
    }
}
