using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace WalletWasabi.Fluent.Controls
{
    public class ProgressRingArc : TemplatedControl
    {
	    private int _pathFigureWidth;
	    private int _pathFigureHeight;
	    private Thickness _pathFigureMargin;
	    private Point _pathFigureStartPoint;
	    private Point _arcSegmentPoint;
	    private Size _arcSegmentSize;
	    private bool _arcSegmentIsLargeArc;

        public static readonly StyledProperty<int> RadiusProperty =
            AvaloniaProperty.Register<ProgressRingArc, int>(nameof(Radius), 35);

        public static readonly StyledProperty<IBrush> SegmentColorProperty =
            AvaloniaProperty.Register<ProgressRingArc, IBrush>(nameof(SegmentColor), Brushes.Gray);

        public static readonly StyledProperty<int> StrokeThicknessProperty =
            AvaloniaProperty.Register<ProgressRingArc, int>(nameof(StrokeThickness), 5);

        public static readonly StyledProperty<double> PercentageProperty =
            AvaloniaProperty.Register<ProgressRingArc, double>(nameof(Percentage),1);

        public static readonly StyledProperty<double> AngleProperty =
            AvaloniaProperty.Register<ProgressRingArc, double>(nameof(Angle),120);

        public static readonly DirectProperty<ProgressRingArc, int> PathFigureWidthProperty =
            AvaloniaProperty.RegisterDirect<ProgressRingArc, int>(nameof(PathFigureWidth),
	            o => o.PathFigureWidth,
	            (o, v) => o.PathFigureWidth = v);

        public static readonly DirectProperty<ProgressRingArc, int> PathFigureHeightProperty =
            AvaloniaProperty.RegisterDirect<ProgressRingArc, int>(nameof(PathFigureHeight),
	            o => o.PathFigureHeight,
	            (o, v) => o.PathFigureHeight = v);

        public static readonly DirectProperty<ProgressRingArc, Thickness> PathFigureMarginProperty =
            AvaloniaProperty.RegisterDirect<ProgressRingArc, Thickness>(nameof(PathFigureMargin),
	            o => o.PathFigureMargin,
	            (o, v) => o.PathFigureMargin = v);

        public static readonly DirectProperty<ProgressRingArc, Point> PathFigureStartPointProperty =
            AvaloniaProperty.RegisterDirect<ProgressRingArc, Point>(nameof(PathFigureStartPoint),
	            o => o.PathFigureStartPoint,
	            (o, v) => o.PathFigureStartPoint = v);

        public static readonly DirectProperty<ProgressRingArc, Point> ArcSegmentPointProperty =
            AvaloniaProperty.RegisterDirect<ProgressRingArc, Point>(nameof(ArcSegmentPoint),
	            o => o.ArcSegmentPoint,
	            (o, v) => o.ArcSegmentPoint = v);

        public static readonly DirectProperty<ProgressRingArc, Size> ArcSegmentSizeProperty =
            AvaloniaProperty.RegisterDirect<ProgressRingArc, Size>(nameof(ArcSegmentSize),
	            o => o.ArcSegmentSize,
	            (o, v) => o.ArcSegmentSize = v);

        public static readonly DirectProperty<ProgressRingArc, bool> ArcSegmentIsLargeArcProperty =
            AvaloniaProperty.RegisterDirect<ProgressRingArc, bool>(nameof(ArcSegmentIsLargeArc),
	            o => o.ArcSegmentIsLargeArc,
	            (o, v) => o.ArcSegmentIsLargeArc = v);

        public int Radius
        {
            get => GetValue(RadiusProperty);
            set => SetValue(RadiusProperty, value);
        }

        public IBrush SegmentColor
        {
            get => GetValue(SegmentColorProperty);
            set => SetValue(SegmentColorProperty, value);
        }

        public int StrokeThickness
        {
            get => GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }

        public double Percentage
        {
            get => GetValue(PercentageProperty);
            set => SetValue(PercentageProperty, value);
        }

        public double Angle
        {
            get => GetValue(AngleProperty);
            set => SetValue(AngleProperty, value);
        }

        public int PathFigureWidth
        {
            get => _pathFigureWidth;
            private set => SetAndRaise(PathFigureWidthProperty, ref _pathFigureWidth, value);
        }

        public int PathFigureHeight
        {
            get => _pathFigureHeight;
            private set => SetAndRaise(PathFigureHeightProperty, ref _pathFigureHeight, value);
        }

        public Thickness PathFigureMargin
        {
            get => _pathFigureMargin;
            private set => SetAndRaise(PathFigureMarginProperty, ref _pathFigureMargin, value);
        }

        public Point PathFigureStartPoint
        {
            get => _pathFigureStartPoint;
            private set => SetAndRaise(PathFigureStartPointProperty, ref _pathFigureStartPoint, value);
        }

        public Point ArcSegmentPoint
        {
            get => _arcSegmentPoint;
            private set => SetAndRaise(ArcSegmentPointProperty, ref _arcSegmentPoint, value);
        }

        public Size ArcSegmentSize
        {
            get => _arcSegmentSize;
            private set => SetAndRaise(ArcSegmentSizeProperty, ref _arcSegmentSize, value);
        }

        public bool ArcSegmentIsLargeArc
        {
            get => _arcSegmentIsLargeArc;
            private set => SetAndRaise(ArcSegmentIsLargeArcProperty, ref _arcSegmentIsLargeArc, value);
        }


        protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
        {
            base.OnPropertyChanged(change);
            Angle = Percentage * 360;
            RenderArc();
        }

        private void RenderArc()
        {
            var startPoint =  new Point(Radius, 0);
            var endPoint = ComputeCartesianCoordinate(Angle, Radius);
            endPoint += new Point(Radius, Radius);

            PathFigureWidth = Radius * 2 + StrokeThickness;
            PathFigureHeight = Radius * 2 + StrokeThickness;
            PathFigureMargin = new Thickness(StrokeThickness, StrokeThickness, 0, 0);

            var largeArc = Angle > 180.0;

            var outerArcSize = new Size(Radius, Radius);

            PathFigureStartPoint = startPoint;

            if (Math.Abs(startPoint.X - Math.Round(endPoint.X)) < 0.01 && Math.Abs(startPoint.Y - Math.Round(endPoint.Y)) < 0.01)
            {
	            endPoint -= new Point(0.01,0);
            }

            ArcSegmentPoint = endPoint;
            ArcSegmentSize = outerArcSize;
            ArcSegmentIsLargeArc = largeArc;
        }

        private static Point ComputeCartesianCoordinate(double angle, double radius)
        {
            // convert to radians
            var angleRad = (Math.PI / 180.0) * (angle - 90);

            var x = radius * Math.Cos(angleRad);
            var y = radius * Math.Sin(angleRad);

            return new Point(x, y);
        }
    }
}
