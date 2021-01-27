// #define DEBUG_LABELS
// #define DEBUG_AXIS_TITLE
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace WalletWasabi.Fluent.Controls
{
	public class LineChart : Control
    {
        #region Avalonia Properties

        #region Values

        public static readonly StyledProperty<List<double>?> ValuesProperty =
            AvaloniaProperty.Register<LineChart, List<double>?>(nameof(Values));

        public static readonly StyledProperty<double> MinValueProperty =
            AvaloniaProperty.Register<LineChart, double>(nameof(MinValue));

        public static readonly StyledProperty<double> MaxValueProperty =
            AvaloniaProperty.Register<LineChart, double>(nameof(MaxValue));

        public static readonly StyledProperty<bool> LogarithmicScaleProperty =
            AvaloniaProperty.Register<LineChart, bool>(nameof(LogarithmicScale));

        public static readonly StyledProperty<IBrush?> FillProperty =
            AvaloniaProperty.Register<LineChart, IBrush?>(nameof(Fill));

        public static readonly StyledProperty<IBrush?> StrokeProperty =
            AvaloniaProperty.Register<LineChart, IBrush?>(nameof(Stroke));

        public static readonly StyledProperty<double> StrokeThicknessProperty =
            AvaloniaProperty.Register<LineChart, double>(nameof(StrokeThickness));

        public static readonly StyledProperty<Thickness> ValuesMarginProperty =
            AvaloniaProperty.Register<LineChart, Thickness>(nameof(ValuesMargin));

        #endregion

        #region Labels

        public static readonly StyledProperty<List<string>?> LabelsProperty =
            AvaloniaProperty.Register<LineChart, List<string>?>(nameof(Labels));

        public static readonly StyledProperty<IBrush?> LabelForegroundProperty =
            AvaloniaProperty.Register<LineChart, IBrush?>(nameof(LabelForeground));

        public static readonly StyledProperty<double> LabelOffsetProperty =
            AvaloniaProperty.Register<LineChart, double>(nameof(LabelOffset));

        public static readonly StyledProperty<double> LabelHeightProperty =
            AvaloniaProperty.Register<LineChart, double>(nameof(LabelHeight));

        public static readonly StyledProperty<TextAlignment> LabelAlignmentProperty =
            AvaloniaProperty.Register<LineChart, TextAlignment>(nameof(LabelAlignment));

        public static readonly StyledProperty<double> LabelAngleProperty =
            AvaloniaProperty.Register<LineChart, double>(nameof(LabelAngle));

        public static readonly StyledProperty<FontFamily> LabelFontFamilyProperty =
            AvaloniaProperty.Register<LineChart, FontFamily>(nameof(LabelFontFamily));

        public static readonly StyledProperty<FontStyle> LabelFontStyleProperty =
            AvaloniaProperty.Register<LineChart, FontStyle>(nameof(LabelFontStyle));

        public static readonly StyledProperty<FontWeight> LabelFontWeightProperty =
            AvaloniaProperty.Register<LineChart, FontWeight>(nameof(LabelFontWeight));

        public static readonly StyledProperty<double> LabelFontSizeProperty =
            AvaloniaProperty.Register<LineChart, double>(nameof(LabelFontSize));

        #endregion

        #region XAxis

        public static readonly StyledProperty<string> XAxisTitleProperty =
            AvaloniaProperty.Register<LineChart, string>(nameof(XAxisTitle));

        public static readonly StyledProperty<IBrush?> XAxisStrokeProperty =
            AvaloniaProperty.Register<LineChart, IBrush?>(nameof(XAxisStroke));

        public static readonly StyledProperty<double> XAxisStrokeThicknessProperty =
            AvaloniaProperty.Register<LineChart, double>(nameof(XAxisStrokeThickness));

        public static readonly StyledProperty<double> XAxisArrowSizeProperty =
            AvaloniaProperty.Register<LineChart, double>(nameof(XAxisArrowSize));

        #endregion

        #region YAxis

        public static readonly StyledProperty<string> YAxisTitleProperty =
            AvaloniaProperty.Register<LineChart, string>(nameof(YAxisTitle));

        public static readonly StyledProperty<IBrush?> YAxisStrokeProperty =
            AvaloniaProperty.Register<LineChart, IBrush?>(nameof(YAxisStroke));

        public static readonly StyledProperty<double> YAxisStrokeThicknessProperty =
            AvaloniaProperty.Register<LineChart, double>(nameof(YAxisStrokeThickness));

        public static readonly StyledProperty<double> YAxisArrowSizeProperty =
            AvaloniaProperty.Register<LineChart, double>(nameof(YAxisArrowSize));

        #endregion

        #region XAxisTitle

        public static readonly StyledProperty<IBrush?> XAxisTitleForegroundProperty =
            AvaloniaProperty.Register<LineChart, IBrush?>(nameof(XAxisTitleForeground));

        public static readonly StyledProperty<double> XAxisTitleOffsetProperty =
            AvaloniaProperty.Register<LineChart, double>(nameof(XAxisTitleOffset));

        public static readonly StyledProperty<double> XAxisTitleHeightProperty =
            AvaloniaProperty.Register<LineChart, double>(nameof(XAxisTitleHeight));

        public static readonly StyledProperty<TextAlignment> XAxisTitleAlignmentProperty =
            AvaloniaProperty.Register<LineChart, TextAlignment>(nameof(XAxisTitleAlignment));

        public static readonly StyledProperty<double> XAxisTitleAngleProperty =
            AvaloniaProperty.Register<LineChart, double>(nameof(XAxisTitleAngle));

        public static readonly StyledProperty<FontFamily> XAxisTitleFontFamilyProperty =
            AvaloniaProperty.Register<LineChart, FontFamily>(nameof(XAxisTitleFontFamily));

        public static readonly StyledProperty<FontStyle> XAxisTitleFontStyleProperty =
            AvaloniaProperty.Register<LineChart, FontStyle>(nameof(XAxisTitleFontStyle));

        public static readonly StyledProperty<FontWeight> XAxisTitleFontWeightProperty =
            AvaloniaProperty.Register<LineChart, FontWeight>(nameof(XAxisTitleFontWeight));

        public static readonly StyledProperty<double> XAxisTitleFontSizeProperty =
            AvaloniaProperty.Register<LineChart, double>(nameof(XAxisTitleFontSize));

        #endregion

        #region YAxisTitle

        public static readonly StyledProperty<IBrush?> YAxisTitleForegroundProperty =
            AvaloniaProperty.Register<LineChart, IBrush?>(nameof(YAxisTitleForeground));

        public static readonly StyledProperty<double> YAxisTitleOffsetProperty =
            AvaloniaProperty.Register<LineChart, double>(nameof(YAxisTitleOffset));

        public static readonly StyledProperty<double> YAxisTitleHeightProperty =
            AvaloniaProperty.Register<LineChart, double>(nameof(YAxisTitleHeight));

        public static readonly StyledProperty<TextAlignment> YAxisTitleAlignmentProperty =
            AvaloniaProperty.Register<LineChart, TextAlignment>(nameof(YAxisTitleAlignment));

        public static readonly StyledProperty<double> YAxisTitleAngleProperty =
            AvaloniaProperty.Register<LineChart, double>(nameof(YAxisTitleAngle));

        public static readonly StyledProperty<FontFamily> YAxisTitleFontFamilyProperty =
            AvaloniaProperty.Register<LineChart, FontFamily>(nameof(YAxisTitleFontFamily));

        public static readonly StyledProperty<FontStyle> YAxisTitleFontStyleProperty =
            AvaloniaProperty.Register<LineChart, FontStyle>(nameof(YAxisTitleFontStyle));

        public static readonly StyledProperty<FontWeight> YAxisTitleFontWeightProperty =
            AvaloniaProperty.Register<LineChart, FontWeight>(nameof(YAxisTitleFontWeight));

        public static readonly StyledProperty<double> YAxisTitleFontSizeProperty =
            AvaloniaProperty.Register<LineChart, double>(nameof(YAxisTitleFontSize));

        #endregion

        #region Cursor

        public static readonly StyledProperty<IBrush?> CursorStrokeProperty =
            AvaloniaProperty.Register<LineChart, IBrush?>(nameof(CursorStroke));

        public static readonly StyledProperty<double> CursorThicknessProperty =
            AvaloniaProperty.Register<LineChart, double>(nameof(CursorThickness));

        public static readonly StyledProperty<double> CursorValueProperty =
            AvaloniaProperty.Register<LineChart, double>(nameof(CursorValue));

        #endregion

        #region Border

        public static readonly StyledProperty<IBrush?> BorderBrushProperty =
            AvaloniaProperty.Register<LineChart, IBrush?>(nameof(BorderBrush));

        public static readonly StyledProperty<double> BorderThicknessProperty =
            AvaloniaProperty.Register<LineChart, double>(nameof(BorderThickness));

        public static readonly StyledProperty<double> BorderRadiusXProperty =
            AvaloniaProperty.Register<LineChart, double>(nameof(BorderRadiusX));

        public static readonly StyledProperty<double> BorderRadiusYProperty =
            AvaloniaProperty.Register<LineChart, double>(nameof(BorderRadiusY));

        #endregion

        #endregion

        #region Fields

        private bool _captured;

        #endregion

        #region static ctor

        static LineChart()
        {
	        AffectsMeasure<LineChart>(StrokeThicknessProperty);
	        AffectsRender<LineChart>(
		        ValuesProperty,
		        LabelsProperty,
		        MinValueProperty,
		        MaxValueProperty,
		        FillProperty,
		        StrokeProperty,
		        StrokeThicknessProperty,
		        LabelForegroundProperty,
		        LabelAngleProperty,
		        CursorStrokeProperty,
		        CursorThicknessProperty,
		        CursorValueProperty);
        }

        #endregion

        #region  ctor

        public LineChart()
        {
	        AddHandler(PointerPressedEvent, PointerPressedHandler, RoutingStrategies.Tunnel);
	        AddHandler(PointerReleasedEvent, PointerReleasedHandler, RoutingStrategies.Tunnel);
	        AddHandler(PointerMovedEvent, PointerMovedHandler, RoutingStrategies.Tunnel);
        }

        #endregion

        #region Clr Properties

        #region Values

        public List<double>? Values
        {
            get => GetValue(ValuesProperty);
            set => SetValue(ValuesProperty, value);
        }

        public double MinValue
        {
            get => GetValue(MinValueProperty);
            set => SetValue(MinValueProperty, value);
        }

        public double MaxValue
        {
            get => GetValue(MaxValueProperty);
            set => SetValue(MaxValueProperty, value);
        }

        public bool LogarithmicScale
        {
            get => GetValue(LogarithmicScaleProperty);
            set => SetValue(LogarithmicScaleProperty, value);
        }

        public IBrush? Fill
        {
            get => GetValue(FillProperty);
            set => SetValue(FillProperty, value);
        }

        public IBrush? Stroke
        {
            get => GetValue(StrokeProperty);
            set => SetValue(StrokeProperty, value);
        }

        public double StrokeThickness
        {
            get => GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }

        public Thickness ValuesMargin
        {
            get => GetValue(ValuesMarginProperty);
            set => SetValue(ValuesMarginProperty, value);
        }

        #endregion

        #region Labels

        public List<string>? Labels
        {
            get => GetValue(LabelsProperty);
            set => SetValue(LabelsProperty, value);
        }

        public IBrush? LabelForeground
        {
            get => GetValue(LabelForegroundProperty);
            set => SetValue(LabelForegroundProperty, value);
        }

        public double LabelAngle
        {
            get => GetValue(LabelAngleProperty);
            set => SetValue(LabelAngleProperty, value);
        }

        public double LabelOffset
        {
            get => GetValue(LabelOffsetProperty);
            set => SetValue(LabelOffsetProperty, value);
        }

        public double LabelHeight
        {
            get => GetValue(LabelHeightProperty);
            set => SetValue(LabelHeightProperty, value);
        }

        public TextAlignment LabelAlignment
        {
            get => GetValue(LabelAlignmentProperty);
            set => SetValue(LabelAlignmentProperty, value);
        }

        public FontFamily LabelFontFamily
        {
            get => GetValue(LabelFontFamilyProperty);
            set => SetValue(LabelFontFamilyProperty, value);
        }

        public FontStyle LabelFontStyle
        {
            get => GetValue(LabelFontStyleProperty);
            set => SetValue(LabelFontStyleProperty, value);
        }

        public FontWeight LabelFontWeight
        {
            get => GetValue(LabelFontWeightProperty);
            set => SetValue(LabelFontWeightProperty, value);
        }

        public double LabelFontSize
        {
            get => GetValue(LabelFontSizeProperty);
            set => SetValue(LabelFontSizeProperty, value);
        }

        #endregion

        #region XAxis

        public string XAxisTitle
        {
            get => GetValue(XAxisTitleProperty);
            set => SetValue(XAxisTitleProperty, value);
        }

        public IBrush? XAxisStroke
        {
            get => GetValue(XAxisStrokeProperty);
            set => SetValue(XAxisStrokeProperty, value);
        }

        public double XAxisStrokeThickness
        {
            get => GetValue(XAxisStrokeThicknessProperty);
            set => SetValue(XAxisStrokeThicknessProperty, value);
        }

        public double XAxisArrowSize
        {
            get => GetValue(XAxisArrowSizeProperty);
            set => SetValue(XAxisArrowSizeProperty, value);
        }

        #endregion

        #region YAxis

        public string YAxisTitle
        {
            get => GetValue(YAxisTitleProperty);
            set => SetValue(YAxisTitleProperty, value);
        }

        public IBrush? YAxisStroke
        {
            get => GetValue(YAxisStrokeProperty);
            set => SetValue(YAxisStrokeProperty, value);
        }

        public double YAxisStrokeThickness
        {
            get => GetValue(YAxisStrokeThicknessProperty);
            set => SetValue(YAxisStrokeThicknessProperty, value);
        }

        public double YAxisArrowSize
        {
            get => GetValue(YAxisArrowSizeProperty);
            set => SetValue(YAxisArrowSizeProperty, value);
        }

        #endregion

        #region XAxisTitle

        public IBrush? XAxisTitleForeground
        {
            get => GetValue(XAxisTitleForegroundProperty);
            set => SetValue(XAxisTitleForegroundProperty, value);
        }

        public double XAxisTitleAngle
        {
            get => GetValue(XAxisTitleAngleProperty);
            set => SetValue(XAxisTitleAngleProperty, value);
        }

        public double XAxisTitleOffset
        {
            get => GetValue(XAxisTitleOffsetProperty);
            set => SetValue(XAxisTitleOffsetProperty, value);
        }

        public double XAxisTitleHeight
        {
            get => GetValue(XAxisTitleHeightProperty);
            set => SetValue(XAxisTitleHeightProperty, value);
        }

        public TextAlignment XAxisTitleAlignment
        {
            get => GetValue(XAxisTitleAlignmentProperty);
            set => SetValue(XAxisTitleAlignmentProperty, value);
        }

        public FontFamily XAxisTitleFontFamily
        {
            get => GetValue(XAxisTitleFontFamilyProperty);
            set => SetValue(XAxisTitleFontFamilyProperty, value);
        }

        public FontStyle XAxisTitleFontStyle
        {
            get => GetValue(XAxisTitleFontStyleProperty);
            set => SetValue(XAxisTitleFontStyleProperty, value);
        }

        public FontWeight XAxisTitleFontWeight
        {
            get => GetValue(XAxisTitleFontWeightProperty);
            set => SetValue(XAxisTitleFontWeightProperty, value);
        }

        public double XAxisTitleFontSize
        {
            get => GetValue(XAxisTitleFontSizeProperty);
            set => SetValue(XAxisTitleFontSizeProperty, value);
        }

        #endregion

        #region YAxisTitle

        public IBrush? YAxisTitleForeground
        {
            get => GetValue(YAxisTitleForegroundProperty);
            set => SetValue(YAxisTitleForegroundProperty, value);
        }

        public double YAxisTitleAngle
        {
            get => GetValue(YAxisTitleAngleProperty);
            set => SetValue(YAxisTitleAngleProperty, value);
        }

        public double YAxisTitleOffset
        {
            get => GetValue(YAxisTitleOffsetProperty);
            set => SetValue(YAxisTitleOffsetProperty, value);
        }

        public double YAxisTitleHeight
        {
            get => GetValue(YAxisTitleHeightProperty);
            set => SetValue(YAxisTitleHeightProperty, value);
        }

        public TextAlignment YAxisTitleAlignment
        {
            get => GetValue(YAxisTitleAlignmentProperty);
            set => SetValue(YAxisTitleAlignmentProperty, value);
        }

        public FontFamily YAxisTitleFontFamily
        {
            get => GetValue(YAxisTitleFontFamilyProperty);
            set => SetValue(YAxisTitleFontFamilyProperty, value);
        }

        public FontStyle YAxisTitleFontStyle
        {
            get => GetValue(YAxisTitleFontStyleProperty);
            set => SetValue(YAxisTitleFontStyleProperty, value);
        }

        public FontWeight YAxisTitleFontWeight
        {
            get => GetValue(YAxisTitleFontWeightProperty);
            set => SetValue(YAxisTitleFontWeightProperty, value);
        }

        public double YAxisTitleFontSize
        {
            get => GetValue(YAxisTitleFontSizeProperty);
            set => SetValue(YAxisTitleFontSizeProperty, value);
        }

        #endregion

        #region Cursor

        public IBrush? CursorStroke
        {
            get => GetValue(CursorStrokeProperty);
            set => SetValue(CursorStrokeProperty, value);
        }

        public double CursorThickness
        {
            get => GetValue(CursorThicknessProperty);
            set => SetValue(CursorThicknessProperty, value);
        }

        public double CursorValue
        {
            get => GetValue(CursorValueProperty);
            set => SetValue(CursorValueProperty, value);
        }

        #endregion

        #region Border

        public IBrush? BorderBrush
        {
            get => GetValue(BorderBrushProperty);
            set => SetValue(BorderBrushProperty, value);
        }

        public double BorderThickness
        {
            get => GetValue(BorderThicknessProperty);
            set => SetValue(BorderThicknessProperty, value);
        }

        public double BorderRadiusX
        {
            get => GetValue(BorderRadiusXProperty);
            set => SetValue(BorderRadiusXProperty, value);
        }

        public double BorderRadiusY
        {
            get => GetValue(BorderRadiusYProperty);
            set => SetValue(BorderRadiusYProperty, value);
        }

        #endregion

        #endregion

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

        private static Geometry CreateFllGeometry(IReadOnlyList<Point> points, double width, double height)
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

        private void UpdateCursorPosition(double x)
        {
	        var rangeValues = MaxValue - MinValue;
	        var rangeArea = Bounds.Width - ValuesMargin.Left - ValuesMargin.Right;
	        var value = Clamp(x - ValuesMargin.Left, 0, rangeArea);
	        CursorValue = MaxValue - rangeValues / rangeArea * value;
        }

        private void PointerMovedHandler(object? sender, PointerEventArgs e)
        {
	        if (_captured)
	        {
		        var position = e.GetPosition(this);
		        UpdateCursorPosition(position.X);
	        }
        }

        private void PointerReleasedHandler(object? sender, PointerReleasedEventArgs e)
        {
	        if (_captured)
	        {
		        Cursor = new Cursor(StandardCursorType.Arrow);
		        _captured = false;
	        }
        }

        private void PointerPressedHandler(object? sender, PointerPressedEventArgs e)
        {
	        var position = e.GetPosition(this);
	        UpdateCursorPosition(position.X);
	        Cursor = new Cursor(StandardCursorType.SizeWestEast);
	        _captured = true;
        }

        private LineChartState CreateChartState(double width, double height)
        {
	        var state = new LineChartState();

	        state.Width = width;
	        state.Height = height;

	        state.AreaMargin = ValuesMargin;
	        state.AreaWidth = width - state.AreaMargin.Left - state.AreaMargin.Right;
	        state.AreaHeight = height - state.AreaMargin.Top - state.AreaMargin.Bottom;

	        var values = Values;
	        if (values is not null)
	        {
		        var logarithmicScale = LogarithmicScale;

		        var valuesList = logarithmicScale ?
			        values.Select(y => Math.Log(y)).ToList()
			        : values.ToList();

		        var valuesListMax = valuesList.Max();
		        var scaledValues = valuesList
			        .Select(y => ScaleVertical(y, valuesListMax, state.AreaHeight))
			        .ToList();

		        state.Step = state.AreaWidth / (scaledValues.Count - 1);
		        state.Points = new Point[scaledValues.Count];

		        for (var i = 0; i < scaledValues.Count; i++)
		        {
			        state.Points[i] = new Point(i * state.Step, scaledValues[i]);
		        }
	        }

	        var labels = Labels;
	        if (labels is not null)
	        {
		        state.Labels = labels.ToList();
	        }

	        var minValue = MinValue;
	        var maxValue = MaxValue;
	        var cursorValue = CursorValue;
	        state.CursorPosition = ScaleHorizontal(maxValue - cursorValue, maxValue, state.AreaWidth);

	        return state;
        }

        private void DrawAreaFill(DrawingContext context, LineChartState state)
        {
	        if (state.Points is null)
	        {
		        return;
	        }
            var brush = Fill;
            if (brush is null)
            {
	            return;
            }
            var deflate = 0.5;
            var geometry = CreateFllGeometry(state.Points, state.AreaWidth, state.AreaHeight);
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
            var brush = Stroke;
            if (brush is null)
            {
	            return;
            }
            var strokeThickness = StrokeThickness;
            var deflate = strokeThickness * 0.5;
            var geometry = CreateStrokeGeometry(state.Points);
            var pen = new Pen(brush, strokeThickness);
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
            var thickness = CursorThickness;
            var pen = new Pen(brush, thickness);
            var deflate = thickness * 0.5;
            var p1 = new Point(state.CursorPosition + deflate, 0);
            var p2 = new Point(state.CursorPosition + deflate, state.AreaHeight);
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
            var size = XAxisArrowSize;
            var thickness = XAxisStrokeThickness;
            var pen = new Pen(brush, thickness);
            var deflate = thickness * 0.5;
            var p1 = new Point(
	            state.AreaMargin.Left + 0.0,
	            state.AreaMargin.Top + state.AreaHeight + deflate);
            var p2 = new Point(
	            state.AreaMargin.Left + state.AreaWidth,
	            state.AreaMargin.Top + state.AreaHeight + deflate);
            context.DrawLine(pen, p1, p2);
            var p3 = new Point(p2.X, p2.Y);
            var p4 = new Point(p2.X - size, p2.Y - size);
            context.DrawLine(pen, p3, p4);
            var p5 = new Point(p2.X, p2.Y);
            var p6 = new Point(p2.X - size, p2.Y + size);
            context.DrawLine(pen, p5, p6);
        }

        private void DrawYAxis(DrawingContext context, LineChartState state)
        {
	        var brush = YAxisStroke;
	        if (brush is null)
	        {
		        return;
	        }
            var size = YAxisArrowSize;
            var thickness = YAxisStrokeThickness;
            var pen = new Pen(brush, thickness);
            var deflate = thickness * 0.5;
            var p1 = new Point(
	            state.AreaMargin.Left / 2 + deflate,
	            state.AreaMargin.Top + 0.0);
            var p2 = new Point(
	            state.AreaMargin.Left / 2 + deflate,
	            state.AreaMargin.Top + state.AreaHeight);
            context.DrawLine(pen, p1, p2);
            var p3 = new Point(p1.X, p1.Y);
            var p4 = new Point(p1.X - size, p1.Y + size);
            context.DrawLine(pen, p3, p4);
            var p5 = new Point(p1.X, p1.Y);
            var p6 = new Point(p1.X + size, p1.Y + size);
            context.DrawLine(pen, p5, p6);
        }

        private void DrawYAxisTitle(DrawingContext context, LineChartState state)
        {
	        var foreground = YAxisTitleForeground;
	        if (foreground is null)
	        {
		        return;
	        }
            var fontFamily = YAxisTitleFontFamily;
            var fontStyle = YAxisTitleFontStyle;
            var fontWeight = YAxisTitleFontWeight;
            var typeface = new Typeface(fontFamily, fontStyle, fontWeight);
            var fontSize = YAxisTitleFontSize;
            var offset = YAxisTitleOffset;
            var constrainHeight = YAxisTitleHeight;
            var angleRadians = Math.PI / 180.0 * YAxisTitleAngle;
            var alignment = YAxisTitleAlignment;
            var title = YAxisTitle;
            var origin = new Point(state.AreaMargin.Left, state.AreaMargin.Top + offset);
            var constraint = new Size(200, 50);
            var formattedText = CreateFormattedText(title, typeface, alignment, fontSize, constraint);
            var xPosition = origin.X;
            var yPosition = origin.Y;
            var matrix = Matrix.CreateTranslation(-xPosition, -yPosition)
                         * Matrix.CreateRotation(angleRadians)
                         * Matrix.CreateTranslation(xPosition, yPosition);
            var transform = context.PushPreTransform(matrix);
            context.DrawText(foreground, origin, formattedText);
#if DEBUG_AXIS_TITLE
            context.DrawRectangle(null, new Pen(new SolidColorBrush(Colors.Magenta)), new Rect(origin, constraint));
#endif
            transform.Dispose();
#if DEBUG_AXIS_TITLE
            context.DrawRectangle(null, new Pen(new SolidColorBrush(Colors.Cyan)), new Rect(origin, constraint));
#endif
        }

        private void DrawLabels(DrawingContext context, LineChartState state)
        {
	        if (state.Labels is null)
	        {
		        return;
	        }
	        var foreground = LabelForeground;
	        if (foreground is null)
	        {
		        return;
	        }
            var fontFamily = LabelFontFamily;
            var fontStyle = LabelFontStyle;
            var fontWeight = LabelFontWeight;
            var typeface = new Typeface(fontFamily, fontStyle, fontWeight);
            var fontSize = LabelFontSize;
            var offset = LabelOffset;
            var labelHeight = LabelHeight;
            var angleRadians = Math.PI / 180.0 * LabelAngle;
            var alignment = LabelAlignment;

            for (var i = 0; i < state.Labels.Count; i++)
            {
                var origin = new Point(
	                i * state.Step - state.Step / 2 + state.AreaMargin.Left,
	                state.AreaHeight + state.AreaMargin.Top + offset);
                var constraint = new Size(state.Step, labelHeight);
                var formattedText = CreateFormattedText(state.Labels[i], typeface, alignment, fontSize, constraint);
                var xPosition = origin.X + state.Step / 2;
                var yPosition = origin.Y + labelHeight / 2;
                var matrix = Matrix.CreateTranslation(-xPosition, -yPosition)
                             * Matrix.CreateRotation(angleRadians)
                             * Matrix.CreateTranslation(xPosition, yPosition);
                var transform = context.PushPreTransform(matrix);
                var offsetCenter = new Point(0, labelHeight / 2 - formattedText.Bounds.Height / 2);
                context.DrawText(foreground, origin + offsetCenter, formattedText);
#if DEBUG_LABELS
                context.DrawRectangle(null, new Pen(new SolidColorBrush(Colors.Magenta)), new Rect(origin, constraint));
#endif
                transform.Dispose();
#if DEBUG_LABELS
                context.DrawRectangle(null, new Pen(new SolidColorBrush(Colors.Cyan)), new Rect(origin, constraint));
#endif
            }
        }

        private void DrawBorder(DrawingContext context, LineChartState state)
        {
            var brush = BorderBrush;
            var thickness = BorderThickness;
            var radiusX = BorderRadiusX;
            var radiusY = BorderRadiusY;
            var pen = new Pen(brush, thickness);
            var rect = new Rect(0, 0, state.Width, state.Height);
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
	        DrawYAxis(context, state);
	        DrawYAxisTitle(context, state);
	        DrawLabels(context, state);
	        DrawBorder(context, state);
        }

        private class LineChartState
        {
	        public double Width { get; set; }
	        public double Height { get; set; }
	        public double AreaWidth { get; set; }
	        public double AreaHeight { get; set; }
	        public Thickness AreaMargin { get; set; }
	        public Point[]? Points { get; set; }
	        public List<string>? Labels { get; set; }
	        public double Step { get; set; }
	        public double CursorPosition { get; set; }
        }
    }
}