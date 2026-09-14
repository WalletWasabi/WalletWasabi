using System;
using Avalonia;
using Avalonia.Media;

namespace WalletWasabi.Fluent.Mobile.Controls;

/// <summary>Builds open circular arcs or seamless full rings. Angles are degrees, clockwise from +X.</summary>
public static class MobileArcGeometry
{
	public static StreamGeometry? Create(Point center, double radius, double startAngle, double sweepAngle)
	{
		if (!double.IsFinite(center.X) || !double.IsFinite(center.Y) || !double.IsFinite(radius) || radius <= 0 ||
			!double.IsFinite(startAngle) || !double.IsFinite(sweepAngle) || sweepAngle == 0)
			return null;

		var start = Math.IEEERemainder(startAngle, 360);
		var sweep = Math.Clamp(sweepAngle, -360, 360);
		var direction = sweep > 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise;
		var radii = new Size(radius, radius);
		Point At(double angle)
		{
			var radians = angle * Math.PI / 180;
			return new Point(center.X + Math.Cos(radians) * radius, center.Y + Math.Sin(radians) * radius);
		}

		var first = At(start);
		var geometry = new StreamGeometry();
		using (var context = geometry.Open())
		{
			context.BeginFigure(first, false);
			if (Math.Abs(sweep) == 360)
			{
				// Equal arc endpoints are degenerate. Two half-arcs close without a cap seam.
				context.ArcTo(At(start + sweep / 2), radii, 0, false, direction);
				context.ArcTo(first, radii, 0, false, direction);
				context.EndFigure(true);
			}
			else
			{
				context.ArcTo(At(start + sweep), radii, 0, Math.Abs(sweep) > 180, direction);
				context.EndFigure(false);
			}
		}
		return geometry;
	}
}
