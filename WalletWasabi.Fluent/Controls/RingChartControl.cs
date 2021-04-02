using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace WalletWasabi.Fluent.Controls
{
	public class RingChartControl : Control
	{
		static RingChartControl()
		{
			AffectsRender<RingChartControl>(DataPointsProperty);
		}

		public static readonly StyledProperty<IList<(IBrush, double)>> DataPointsProperty =
			AvaloniaProperty.Register<RingChartControl, IList<(IBrush, double)>>("DataPoints");

		public static readonly StyledProperty<double> RadiusProperty =
			AvaloniaProperty.Register<RingChartControl, double>("Radius", 60);

		public static readonly StyledProperty<double> StartAngleProperty =
			AvaloniaProperty.Register<RingChartControl, double>("StartAngle", 0);

		public static readonly StyledProperty<double> EndAngleProperty =
			AvaloniaProperty.Register<RingChartControl, double>("EndAngle", 360);

		public static readonly StyledProperty<Point> CenterProperty =
			AvaloniaProperty.Register<RingChartControl, Point>("Center");

		public static readonly StyledProperty<double> ThicknessProperty =
			AvaloniaProperty.Register<RingChartControl, double>("Thickness", 6);

		public IList<(IBrush color, double value)> DataPoints
		{
			get => GetValue(DataPointsProperty);
			set => SetValue(DataPointsProperty, value);
		}

		public double Map(double value, double from1, double to1, double from2, double to2)
		{
			return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
		}

		public Point PolarToCartesian(double angleRad, double radius)
		{
			double x = radius * Math.Cos(angleRad);
			double y = radius * Math.Sin(angleRad);
			return new Point(x, y);
		}

		private Geometry GetArcGeometry(double startAngle, double endAngle, bool isPathClosed = false)
		{
			var startRadians = ConvertToRadians(startAngle) - (Math.PI / 2);
			var endRadians = ConvertToRadians(endAngle) - (Math.PI / 2);

			if (endRadians < startRadians)
			{
				endRadians += Math.PI * 2;
			}

			var d = SweepDirection.Clockwise;
			var largeArc = (endAngle - startAngle) > 180.0;

			Point p0 = Center + new Vector(Math.Cos(startRadians), Math.Sin(startRadians)) * Radius;
			Point p1 = Center + new Vector(Math.Cos(endRadians), Math.Sin(endRadians)) * Radius;

			PathSegments segments = new()
			{
				new ArcSegment
				{
					IsLargeArc = largeArc,
					Size = new Size(Radius, Radius),
					Point = p1,
					SweepDirection = d
				}
			};

			PathFigures figures = new()
			{
				new PathFigure
				{
					StartPoint = p0,
					Segments = segments,
					IsClosed = isPathClosed
				}
			};

			return new PathGeometry
			{
				Figures = figures,
				FillRule = FillRule.EvenOdd
			};
		}

		private double ConvertToRadians(double angle)
		{
			return (Math.PI / 180d) * angle;
		}

		public double Radius
		{
			get => GetValue(RadiusProperty);
			set => SetValue(RadiusProperty, value);
		}

		public double StartAngle
		{
			get => GetValue(StartAngleProperty);
			set => SetValue(StartAngleProperty, value);
		}

		public double EndAngle
		{
			get => GetValue(EndAngleProperty);
			set => SetValue(EndAngleProperty, value);
		}

		public Point Center
		{
			get => GetValue(CenterProperty);
			set => SetValue(CenterProperty, value);
		}

		public double Thickness
		{
			get => GetValue(ThicknessProperty);
			set => SetValue(ThicknessProperty, value);
		}

		public override void Render(DrawingContext context)
		{
			if (DataPoints is null)
			{
				return;
			}

			var total = DataPoints.Sum(x => x.value);

			if (total <= 1.0)
			{
				var lastAngle = StartAngle;
				var lastData = 0d;

				foreach (var dataPoint in DataPoints.OrderBy(x=>x.value))
				{
					lastData += dataPoint.value;
					var curValue = Map((dataPoint.value == 1.0 ? 0.99 : lastData), 0, 1, StartAngle, EndAngle);

					context.DrawGeometry(null, new ImmutablePen(dataPoint.color, Thickness), GetArcGeometry(lastAngle, curValue, (dataPoint.value == 1.0d)));

					lastAngle = curValue;
				}
			}

			base.Render(context);
		}
	}
}