using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevZH.UI.Drawing
{
    public class Brush
    {
        internal BrushInternal Internal = new BrushInternal();

        public BrushType BrushType
        {
            get { return Internal.BrushType; }
            protected set { Internal.BrushType = value; }
        }
    }

    public abstract class GradientBrush : Brush
    {
        protected double X0 { get { return Internal.X0; } set { Internal.X0 = value; } }      // linear: start X, radial: start X
        protected double Y0 { get { return Internal.Y0; } set { Internal.Y0 = value; } }      // linear: start Y, radial: start Y
        protected double X1 { get { return Internal.X1; } set { Internal.X1 = value; } }      // linear: end X, radial: outer circle center X
        protected double Y1 { get { return Internal.Y1; } set { Internal.Y1 = value; } }      // linear: end Y, radial: outer circle center Y


        public GradientStop[] Stops
        {
            set
            {
                if (value != null && value.Length != 0)
                {
                    var length = value.Length;
                    Internal.Stops = Marshal.UnsafeAddrOfPinnedArrayElement(value, 0);
                    Internal.NumStops = (UIntPtr)length;
                }
            }
        }
    }

    public sealed class LinearGradientBrush : GradientBrush
    {
        public LinearGradientBrush()
        {
            Internal.BrushType = BrushType.LinearGradient;
        }

        private Point _start = new Point();

        public Point StartPoint
        {
            get
            {
                _start.X = this.X0;
                _start.Y = this.Y0;
                return _start;
            }
            set
            {
                if (_start != value)
                {
                    _start = value;
                    this.X0 = value.X;
                    this.Y0 = value.Y;
                }
            }
        }

        private Point _end = new Point(1, 1);
        public Point EndPoint
        {
            get
            {
                _end.X = this.X1;
                _end.Y = this.Y1;
                return _end;
            }
            set
            {
                if (_end != value)
                {
                    _end = value;
                    this.X1 = value.X;
                    this.Y1 = value.Y;
                }
            }
        }
    }

    public sealed class RadialGradientBrush : GradientBrush
    {
        public RadialGradientBrush()
        {
            Internal.BrushType = BrushType.RadialGradient;
        }

        private Point _origin = new Point(0.5, 0.5);

        public Point GradientOrigin
        {
            get
            {
                _origin.X = this.X0;
                _origin.Y = this.Y0;
                return _origin;
            }
            set
            {
                if (_origin != value)
                {
                    _origin = value;
                    this.X0 = value.X;
                    this.Y0 = value.Y;
                }
            }
        }

        private Point _center = new Point(0.5, 0.5);
        public Point Center
        {
            get
            {
                _center.X = this.X1;
                _center.Y = this.Y1;
                return _center;
            }
            set
            {
                if (_center != value)
                {
                    _center = value;
                    this.X1 = value.X;
                    this.Y1 = value.Y;
                }
            }
        }

        public double OuterRadius { get { return Internal.OuterRadius; } set { Internal.OuterRadius = value; } }		// radial gradients only
    }

    public sealed class SolidColorBrush : Brush
    {
        public double R { get { return Internal.R; } set { Internal.R = value; } }
        public double G { get { return Internal.G; } set { Internal.G = value; } }
        public double B { get { return Internal.B; } set { Internal.B = value; } }
        public double A { get { return Internal.A; } set { Internal.A = value; } }

        public SolidColorBrush()
        {
            Internal.BrushType = BrushType.Solid;
        }

        internal SolidColorBrush(Color color) : this()
        {
            R = color.R;
            G = color.G;
            B = color.B;
            A = color.A;
        }

        public static explicit operator Color(SolidColorBrush brush)
        {
            var color = new Color(){R = brush.R, G = brush.G, B = brush.B, A = brush.A};
            return color;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal /*unsafe*/ struct BrushInternal
    {
        [MarshalAs(UnmanagedType.I4)]
        public BrushType BrushType;

        // solid brushes
        public double R;
        public double G;
        public double B;
        public double A;

        // gradient brushes
        public double X0;      // linear: start X, radial: start X
        public double Y0;      // linear: start Y, radial: start Y
        public double X1;      // linear: end X, radial: outer circle center X
        public double Y1;      // linear: end Y, radial: outer circle center Y
        public double OuterRadius;		// radial gradients only

        //public GradientStop* Stops;
        public IntPtr Stops;

        //[MarshalAs(UnmanagedType.SysUInt)]
        //public uint NumStops;
        public UIntPtr NumStops;

        // TODO extend mode
        // cairo: none, repeat, reflect, pad; no individual control
        // Direct2D: repeat, reflect, pad; no individual control
        // Core Graphics: none, pad; before and after individually
        // TODO cairo documentation is inconsistent about pad

        // TODO images

        // TODO transforms
    }
}
