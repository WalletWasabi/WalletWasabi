// #define DEBUG_LABELS
// #define DEBUG_AXIS_TITLE

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace WalletWasabi.Fluent.Controls
{
	public partial class LineChart : Control
	{
		public LineChart()
		{
			AddHandler(PointerPressedEvent, PointerPressedHandler, RoutingStrategies.Tunnel);
			AddHandler(PointerReleasedEvent, PointerReleasedHandler, RoutingStrategies.Tunnel);
			AddHandler(PointerMovedEvent, PointerMovedHandler, RoutingStrategies.Tunnel);
			AddHandler(PointerLeaveEvent, PointerLeaveHandler, RoutingStrategies.Tunnel);
		}

		private static double Clamp(double val, double min, double max)
		{
			return Math.Min(Math.Max(val, min), max);
		}

		private static double ScaleHorizontal(double value, double max, double range)
		{
			return value / max * range;
		}

		private static double ScaleVertical(double value, double max, double range)
		{
			return range - value / max * range;
		}

		private static Geometry CreateFillGeometry(IReadOnlyList<Point> points, double width, double height)
		{
			var geometry = new StreamGeometry();
			using var context = geometry.Open();
			context.BeginFigure(points[0], true);
			for (var i = 1; i < points.Count; i++)
			{
				context.LineTo(points[i]);
			}

			context.LineTo(new Point(width, height));
			context.LineTo(new Point(0, height));
			context.EndFigure(true);
			return geometry;
		}

		private static Geometry CreateStrokeGeometry(IReadOnlyList<Point> points)
		{
			var geometry = new StreamGeometry();
			using var context = geometry.Open();
			context.BeginFigure(points[0], false);
			for (var i = 1; i < points.Count; i++)
			{
				context.LineTo(points[i]);
			}

			context.EndFigure(false);
			return geometry;
		}

		private static FormattedText CreateFormattedText(string text, Typeface typeface, TextAlignment alignment, double fontSize, Size constraint)
		{
			return new FormattedText()
			{
				Typeface = typeface,
				Text = text,
				TextAlignment = alignment,
				TextWrapping = TextWrapping.NoWrap,
				FontSize = fontSize,
				Constraint = constraint
			};
		}

		private void UpdateXAxisCursorPosition(double x)
		{
			var rangeValues = XAxisMaxValue - XAxisMinValue;
			var rangeArea = Bounds.Width - AreaMargin.Left - AreaMargin.Right;
			var value = Clamp(x - AreaMargin.Left, 0, rangeArea);
			XAxisCurrentValue = XAxisMaxValue - rangeValues / rangeArea * value;
		}

		private Rect GetXAxisCursorHitTestRect()
		{
			var chartWidth = Bounds.Width;
			var chartHeight = Bounds.Height;
			var areaMargin = AreaMargin;
			var areaWidth = chartWidth - areaMargin.Left - areaMargin.Right;
			var areaHeight = chartHeight - areaMargin.Top - areaMargin.Bottom;
			var areaRect = new Rect(areaMargin.Left, areaMargin.Top, areaWidth, areaHeight);
			var cursorValue = XAxisCurrentValue;
			var cursorPosition = ScaleHorizontal(XAxisMaxValue - cursorValue, XAxisMaxValue, areaWidth);
			var cursorHitTestSize = 5;
			var cursorStrokeThickness = CursorStrokeThickness;
			var cursorHitTestRect = new Rect(
				areaMargin.Left + cursorPosition - cursorHitTestSize + cursorStrokeThickness / 2,
				areaRect.Top,
				cursorHitTestSize + cursorHitTestSize,
				areaRect.Height);
			return cursorHitTestRect;
		}

		private void PointerLeaveHandler(object? sender, PointerEventArgs e)
		{
			Cursor = new Cursor(StandardCursorType.Arrow);
		}

		private void PointerMovedHandler(object? sender, PointerEventArgs e)
		{
			var position = e.GetPosition(this);
			if (_captured)
			{
				UpdateXAxisCursorPosition(position.X);
			}
			else
			{
				if (CursorStroke is null)
				{
					return;
				}

				var cursorHitTestRect = GetXAxisCursorHitTestRect();
				var cursorSizeWestEast = cursorHitTestRect.Contains(position);
				Cursor = cursorSizeWestEast ?
					new Cursor(StandardCursorType.SizeWestEast)
					: new Cursor(StandardCursorType.Arrow);
			}
		}

		private void PointerReleasedHandler(object? sender, PointerReleasedEventArgs e)
		{
			if (_captured)
			{
				var position = e.GetPosition(this);
				var cursorHitTestRect = GetXAxisCursorHitTestRect();
				var cursorSizeWestEast = cursorHitTestRect.Contains(position);
				if (!cursorSizeWestEast)
				{
					Cursor = new Cursor(StandardCursorType.Arrow);
				}
				_captured = false;
			}
		}

		private void PointerPressedHandler(object? sender, PointerPressedEventArgs e)
		{
			var position = e.GetPosition(this);
			UpdateXAxisCursorPosition(position.X);
			Cursor = new Cursor(StandardCursorType.SizeWestEast);
			_captured = true;
		}

		private LineChartState CreateChartState(double width, double height)
		{
			var state = new LineChartState();

			// Char Size

			state.ChartWidth = width;
			state.ChartHeight = height;

			// Char Area

			state.AreaMargin = AreaMargin;
			state.AreaWidth = width - state.AreaMargin.Left - state.AreaMargin.Right;
			state.AreaHeight = height - state.AreaMargin.Top - state.AreaMargin.Bottom;

			// X & Y Axis Values

			var yAxisValues = YAxisValues;
			if (yAxisValues is not null && yAxisValues.Count > 1)
			{
				var logarithmicScale = YAxisLogarithmicScale;

				var valuesList = logarithmicScale
					? yAxisValues.Select(y => Math.Log(y)).ToList()
					: yAxisValues.ToList();

				var valuesListMax = valuesList.Max();
				var scaledValues = valuesList
					.Select(y => ScaleVertical(y, valuesListMax, state.AreaHeight))
					.ToList();

				state.XAxisStep = state.AreaWidth / (scaledValues.Count - 1);
				state.Points = new Point[scaledValues.Count];

				for (var i = 0; i < scaledValues.Count; i++)
				{
					state.Points[i] = new Point(i * state.XAxisStep, scaledValues[i]);
				}
			}
			else
			{
				state.XAxisStep = double.NaN;
				state.Points = null;
			}

			// TODO: XAxisValues
			// TODO: YAxisStep

			// X Axis Labels

			var xAxisLabels = XAxisLabels;
			if (xAxisLabels is not null)
			{
				state.XAxisLabels = xAxisLabels.ToList();
			}
			else
			{
				if (XAxisStroke is not null && XAxisValues is not null)
				{
					state.XAxisLabels = XAxisValues.Select(x => x.ToString(CultureInfo.InvariantCulture)).ToList();
				}
			}

			// Y Axis Labels

			var yAxisLabels = YAxisLabels;
			if (yAxisLabels is not null)
			{
				state.YAxisLabels = yAxisLabels.ToList();
			}
			else
			{
				if (YAxisStroke is not null && YAxisValues is not null)
				{
					state.YAxisLabels = YAxisValues.Select(x => x.ToString(CultureInfo.InvariantCulture)).ToList();
				}
			}

			// X Axis Cursor Position

			var xAxisMinValue = XAxisMinValue;
			var xAxisMaxValue = XAxisMaxValue;
			var xAxisCurrentValue = XAxisCurrentValue;
			state.XAxisCursorPosition = ScaleHorizontal(xAxisMaxValue - xAxisCurrentValue, xAxisMaxValue, state.AreaWidth);

			return state;
		}

		private void DrawAreaFill(DrawingContext context, LineChartState state)
		{
			if (state.Points is null)
			{
				return;
			}

			var brush = AreaFill;
			if (brush is null)
			{
				return;
			}

			if (state.AreaWidth <= 0
			    || state.AreaHeight <= 0
			    || state.AreaWidth < AreaMinViableWidth
			    || state.AreaHeight < AreaMinViableHeight)
			{
				return;
			}

			var deflate = 0.5;
			var geometry = CreateFillGeometry(state.Points, state.AreaWidth, state.AreaHeight);
			var transform = context.PushPreTransform(
				Matrix.CreateTranslation(
					state.AreaMargin.Left + deflate,
					state.AreaMargin.Top + deflate));
			context.DrawGeometry(brush, null, geometry);
			transform.Dispose();
		}

		private void DrawAreaStroke(DrawingContext context, LineChartState state)
		{
			if (state.Points is null)
			{
				return;
			}

			var brush = AreaStroke;
			if (brush is null)
			{
				return;
			}

			if (state.AreaWidth <= 0
			    || state.AreaHeight <= 0
			    || state.AreaWidth < AreaMinViableWidth
			    || state.AreaHeight < AreaMinViableHeight)
			{
				return;
			}

			var thickness = AreaStrokeThickness;
			var dashStyle = AreaStrokeDashStyle;
			var lineCap = AreaStrokeLineCap;
			var lineJoin = AreaStrokeLineJoin;
			var miterLimit = AreaStrokeMiterLimit;
			var pen = new Pen(brush, thickness, dashStyle, lineCap, lineJoin, miterLimit);
			var deflate = thickness * 0.5;
			var geometry = CreateStrokeGeometry(state.Points);
			var transform = context.PushPreTransform(
				Matrix.CreateTranslation(
					state.AreaMargin.Left + deflate,
					state.AreaMargin.Top + deflate));
			context.DrawGeometry(null, pen, geometry);
			transform.Dispose();
		}

		private void DrawCursor(DrawingContext context, LineChartState state)
		{
			var brush = CursorStroke;
			if (brush is null)
			{
				return;
			}

			if (state.AreaWidth <= 0
			    || state.AreaHeight <= 0
			    || state.AreaWidth < AreaMinViableWidth
			    || state.AreaHeight < AreaMinViableHeight)
			{
				return;
			}

			var thickness = CursorStrokeThickness;
			var dashStyle = CursorStrokeDashStyle;
			var lineCap = CursorStrokeLineCap;
			var lineJoin = CursorStrokeLineJoin;
			var miterLimit = CursorStrokeMiterLimit;
			var pen = new Pen(brush, thickness, dashStyle, lineCap, lineJoin, miterLimit);
			var deflate = thickness * 0.5;
			var p1 = new Point(state.XAxisCursorPosition + deflate, 0);
			var p2 = new Point(state.XAxisCursorPosition + deflate, state.AreaHeight);
			var transform = context.PushPreTransform(
				Matrix.CreateTranslation(
					state.AreaMargin.Left,
					state.AreaMargin.Top));
			context.DrawLine(pen, p1, p2);
			transform.Dispose();
		}

		private void DrawXAxis(DrawingContext context, LineChartState state)
		{
			var brush = XAxisStroke;
			if (brush is null)
			{
				return;
			}

			if (state.AreaWidth <= 0
			    || state.AreaHeight <= 0
			    || state.AreaWidth < XAxisMinViableWidth
			    || state.AreaHeight < XAxisMinViableHeight)
			{
				return;
			}

			var size = XAxisArrowSize;
			var opacity = XAxisOpacity;
			var thickness = XAxisStrokeThickness;
			var pen = new Pen(brush, thickness, null, PenLineCap.Round, PenLineJoin.Miter, 10.0);
			var deflate = thickness * 0.5;
			var offset = XAxisOffset;
			var p1 = new Point(
				state.AreaMargin.Left + offset.X,
				state.AreaMargin.Top + state.AreaHeight + offset.Y + deflate);
			var p2 = new Point(
				state.AreaMargin.Left + state.AreaWidth,
				state.AreaMargin.Top + state.AreaHeight + offset.Y + deflate);
			var opacityState = context.PushOpacity(opacity);
			context.DrawLine(pen, p1, p2);
			var p3 = new Point(p2.X, p2.Y);
			var p4 = new Point(p2.X - size, p2.Y - size);
			context.DrawLine(pen, p3, p4);
			var p5 = new Point(p2.X, p2.Y);
			var p6 = new Point(p2.X - size, p2.Y + size);
			context.DrawLine(pen, p5, p6);
			opacityState.Dispose();
		}

		private void DrawXAxisLabels(DrawingContext context, LineChartState state)
		{
			if (state.XAxisLabels is null)
			{
				return;
			}

			if (double.IsNaN(state.XAxisStep))
			{
				return;
			}

			var foreground = XAxisLabelForeground;
			if (foreground is null)
			{
				return;
			}

			if (state.ChartWidth <= 0
			    || state.ChartHeight <= 0
			    || state.ChartHeight - state.AreaMargin.Top < state.AreaMargin.Bottom)
			{
				return;
			}

			var opacity = XAxisLabelOpacity;
			var fontFamily = XAxisLabelFontFamily;
			var fontStyle = XAxisLabelFontStyle;
			var fontWeight = XAxisLabelFontWeight;
			var typeface = new Typeface(fontFamily, fontStyle, fontWeight);
			var fontSize = XAxisLabelFontSize;
			var offset = XAxisLabelOffset;
			var size = XAxisLabelSize;
			var angleRadians = Math.PI / 180.0 * XAxisLabelAngle;
			var alignment = XAxisLabelAlignment;
			var originTop = state.AreaHeight + state.AreaMargin.Top;
			var offsetTransform = context.PushPreTransform(Matrix.CreateTranslation(offset.X, offset.Y));
			for (var i = 0; i < state.XAxisLabels.Count; i++)
			{
				var origin = new Point(i * state.XAxisStep - size.Width / 2 + state.AreaMargin.Left, originTop);
				var constraint = new Size(size.Width, size.Height);
				var formattedText = CreateFormattedText(state.XAxisLabels[i], typeface, alignment, fontSize, constraint);
				var xPosition = origin.X + size.Width / 2;
				var yPosition = origin.Y + size.Height / 2;
				var matrix = Matrix.CreateTranslation(-xPosition, -yPosition)
				             * Matrix.CreateRotation(angleRadians)
				             * Matrix.CreateTranslation(xPosition, yPosition);
				var labelTransform = context.PushPreTransform(matrix);
				var offsetCenter = new Point(0, size.Height / 2 - formattedText.Bounds.Height / 2);
				var opacityState = context.PushOpacity(opacity);
				context.DrawText(foreground, origin + offsetCenter, formattedText);
#if DEBUG_LABELS
                context.DrawRectangle(null, new Pen(new SolidColorBrush(Colors.Magenta)), new Rect(origin, constraint));
#endif
				opacityState.Dispose();
				labelTransform.Dispose();
#if DEBUG_LABELS
                context.DrawRectangle(null, new Pen(new SolidColorBrush(Colors.Cyan)), new Rect(origin, constraint));
#endif
			}

			offsetTransform.Dispose();
		}

		private void DrawXAxisTitle(DrawingContext context, LineChartState state)
		{
			// TODO: Draw XAxis title.
		}

		private void DrawYAxis(DrawingContext context, LineChartState state)
		{
			var brush = YAxisStroke;
			if (brush is null)
			{
				return;
			}

			if (state.AreaWidth <= 0
			    || state.AreaHeight <= 0
			    || state.AreaWidth < YAxisMinViableWidth
			    || state.AreaHeight < YAxisMinViableHeight)
			{
				return;
			}

			var size = YAxisArrowSize;
			var opacity = YAxisOpacity;
			var thickness = YAxisStrokeThickness;
			var pen = new Pen(brush, thickness, null, PenLineCap.Round, PenLineJoin.Miter, 10.0);
			var deflate = thickness * 0.5;
			var offset = YAxisOffset;
			var p1 = new Point(
				state.AreaMargin.Left + offset.X + deflate,
				state.AreaMargin.Top);
			var p2 = new Point(
				state.AreaMargin.Left + offset.X + deflate,
				state.AreaMargin.Top + state.AreaHeight + offset.Y);
			var opacityState = context.PushOpacity(opacity);
			context.DrawLine(pen, p1, p2);
			var p3 = new Point(p1.X, p1.Y);
			var p4 = new Point(p1.X - size, p1.Y + size);
			context.DrawLine(pen, p3, p4);
			var p5 = new Point(p1.X, p1.Y);
			var p6 = new Point(p1.X + size, p1.Y + size);
			context.DrawLine(pen, p5, p6);
			opacityState.Dispose();
		}

		private void DrawYAxisLabels(DrawingContext context, LineChartState state)
		{
			// TODO: Draw YAxis labels.
		}

		private void DrawYAxisTitle(DrawingContext context, LineChartState state)
		{
			var foreground = YAxisTitleForeground;
			if (foreground is null)
			{
				return;
			}

			if (state.AreaWidth <= 0
			    || state.AreaHeight <= 0
			    || state.AreaWidth < YAxisMinViableWidth
			    || state.AreaHeight < YAxisMinViableHeight)
			{
				return;
			}

			var opacity = YAxisTitleOpacity;
			var fontFamily = YAxisTitleFontFamily;
			var fontStyle = YAxisTitleFontStyle;
			var fontWeight = YAxisTitleFontWeight;
			var typeface = new Typeface(fontFamily, fontStyle, fontWeight);
			var fontSize = YAxisTitleFontSize;
			var offset = YAxisTitleOffset;
			var size = YAxisTitleSize;
			var angleRadians = Math.PI / 180.0 * YAxisTitleAngle;
			var alignment = YAxisTitleAlignment;
			var offsetTransform = context.PushPreTransform(Matrix.CreateTranslation(offset.X, offset.Y));
			var origin = new Point(state.AreaMargin.Left, state.AreaHeight + state.AreaMargin.Top);
			var constraint = new Size(size.Width, size.Height);
			var formattedText = CreateFormattedText(YAxisTitle, typeface, alignment, fontSize, constraint);
			var xPosition = origin.X + size.Width / 2;
			var yPosition = origin.Y + size.Height / 2;
			var matrix = Matrix.CreateTranslation(-xPosition, -yPosition)
			             * Matrix.CreateRotation(angleRadians)
			             * Matrix.CreateTranslation(xPosition, yPosition);
			var labelTransform = context.PushPreTransform(matrix);
			var offsetCenter = new Point(0, size.Height / 2 - formattedText.Bounds.Height / 2);
			var opacityState = context.PushOpacity(opacity);
			context.DrawText(foreground, origin + offsetCenter, formattedText);
#if DEBUG_AXIS_TITLE
            context.DrawRectangle(null, new Pen(new SolidColorBrush(Colors.Magenta)), new Rect(origin, constraint));
#endif
			opacityState.Dispose();
			labelTransform.Dispose();
#if DEBUG_AXIS_TITLE
            context.DrawRectangle(null, new Pen(new SolidColorBrush(Colors.Cyan)), new Rect(origin, constraint));
#endif
			offsetTransform.Dispose();
		}

		private void DrawBorder(DrawingContext context, LineChartState state)
		{
			var brush = BorderBrush;
			if (brush is null)
			{
				return;
			}

			if (state.AreaWidth <= 0 || state.AreaHeight <= 0)
			{
				return;
			}

			var thickness = BorderThickness;
			var radiusX = BorderRadiusX;
			var radiusY = BorderRadiusY;
			var pen = new Pen(brush, thickness, null, PenLineCap.Round, PenLineJoin.Miter, 10.0);
			var rect = new Rect(0, 0, state.ChartWidth, state.ChartHeight);
			var rectDeflate = rect.Deflate(thickness * 0.5);
			context.DrawRectangle(Brushes.Transparent, pen, rectDeflate, radiusX, radiusY);
		}

		public override void Render(DrawingContext context)
		{
			base.Render(context);

			var state = CreateChartState(Bounds.Width, Bounds.Height);

			DrawAreaFill(context, state);
			DrawAreaStroke(context, state);
			DrawCursor(context, state);

			DrawXAxis(context, state);
			DrawXAxisTitle(context, state);
			DrawXAxisLabels(context, state);

			DrawYAxis(context, state);
			DrawYAxisTitle(context, state);
			DrawXAxisLabels(context, state);

			DrawBorder(context, state);
		}

		private class LineChartState
		{
			public double ChartWidth { get; set; }
			public double ChartHeight { get; set; }
			public double AreaWidth { get; set; }
			public double AreaHeight { get; set; }
			public Thickness AreaMargin { get; set; }
			public Point[]? Points { get; set; }
			public List<string>? XAxisLabels { get; set; }
			public double XAxisStep { get; set; }
			public List<string>? YAxisLabels { get; set; }
			public double YAxisStep { get; set; }
			public double XAxisCursorPosition { get; set; }
		}
	}
}