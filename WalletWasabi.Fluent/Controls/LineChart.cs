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

        #region Area

        public static readonly StyledProperty<Thickness> AreaMarginProperty =
	        AvaloniaProperty.Register<LineChart, Thickness>(nameof(AreaMargin));

        public static readonly StyledProperty<IBrush?> AreaFillProperty =
            AvaloniaProperty.Register<LineChart, IBrush?>(nameof(AreaFill));

        public static readonly StyledProperty<IBrush?> AreaStrokeProperty =
            AvaloniaProperty.Register<LineChart, IBrush?>(nameof(AreaStroke));

        public static readonly StyledProperty<double> AreaStrokeThicknessProperty =
            AvaloniaProperty.Register<LineChart, double>(nameof(AreaStrokeThickness), 1.0);

        public static readonly StyledProperty<IDashStyle?> AreaStrokeDashStyleProperty =
	        AvaloniaProperty.Register<LineChart, IDashStyle?>(nameof(AreaStrokeDashStyle), null);

        public static readonly StyledProperty<PenLineCap> AreaStrokeLineCapProperty =
	        AvaloniaProperty.Register<LineChart, PenLineCap>(nameof(AreaStrokeLineCap), PenLineCap.Flat);

        public static readonly StyledProperty<PenLineJoin> AreaStrokeLineJoinProperty =
	        AvaloniaProperty.Register<LineChart, PenLineJoin>(nameof(AreaStrokeLineJoin), PenLineJoin.Miter);

        public static readonly StyledProperty<double> AreaStrokeMiterLimitProperty =
	        AvaloniaProperty.Register<LineChart, double>(nameof(AreaStrokeMiterLimit), 10.0);

        #endregion

        #region XAxis

        public static readonly StyledProperty<List<double>?> XAxisValuesProperty =
	        AvaloniaProperty.Register<LineChart, List<double>?>(nameof(XAxisValues));

        public static readonly StyledProperty<List<string>?> XAxisLabelsProperty =
	        AvaloniaProperty.Register<LineChart, List<string>?>(nameof(XAxisLabels));

        public static readonly StyledProperty<IBrush?> XAxisStrokeProperty =
            AvaloniaProperty.Register<LineChart, IBrush?>(nameof(XAxisStroke));

        public static readonly StyledProperty<double> XAxisStrokeThicknessProperty =
            AvaloniaProperty.Register<LineChart, double>(nameof(XAxisStrokeThickness));

        public static readonly StyledProperty<double> XAxisArrowSizeProperty =
            AvaloniaProperty.Register<LineChart, double>(nameof(XAxisArrowSize));

        #endregion

        #region XAxis Label

        public static readonly StyledProperty<IBrush?> XAxisLabelForegroundProperty =
	        AvaloniaProperty.Register<LineChart, IBrush?>(nameof(XAxisLabelForeground));

        public static readonly StyledProperty<double> XAxisLabelOpacityProperty =
	        AvaloniaProperty.Register<LineChart, double>(nameof(XAxisLabelOpacity));

        public static readonly StyledProperty<Point> XAxisLabelOffsetProperty =
	        AvaloniaProperty.Register<LineChart, Point>(nameof(XAxisLabelOffset));

        public static readonly StyledProperty<Size> XAxisLabelSizeProperty =
	        AvaloniaProperty.Register<LineChart, Size>(nameof(XAxisLabelSize));

        public static readonly StyledProperty<TextAlignment> XAxisLabelAlignmentProperty =
	        AvaloniaProperty.Register<LineChart, TextAlignment>(nameof(XAxisLabelAlignment));

        public static readonly StyledProperty<double> XAxisLabelAngleProperty =
	        AvaloniaProperty.Register<LineChart, double>(nameof(XAxisLabelAngle));

        public static readonly StyledProperty<FontFamily> XAxisLabelFontFamilyProperty =
	        AvaloniaProperty.Register<LineChart, FontFamily>(nameof(XAxisLabelFontFamily));

        public static readonly StyledProperty<FontStyle> XAxisLabelFontStyleProperty =
	        AvaloniaProperty.Register<LineChart, FontStyle>(nameof(XAxisLabelFontStyle));

        public static readonly StyledProperty<FontWeight> XAxisLabelFontWeightProperty =
	        AvaloniaProperty.Register<LineChart, FontWeight>(nameof(XAxisLabelFontWeight));

        public static readonly StyledProperty<double> XAxisLabelFontSizeProperty =
	        AvaloniaProperty.Register<LineChart, double>(nameof(XAxisLabelFontSize));

        #endregion

        #region XAxis Title

        public static readonly StyledProperty<string> XAxisTitleProperty =
	        AvaloniaProperty.Register<LineChart, string>(nameof(XAxisTitle));

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

        #region YAxis

        public static readonly StyledProperty<List<double>?> YAxisValuesProperty =
	        AvaloniaProperty.Register<LineChart, List<double>?>(nameof(YValues));

        public static readonly StyledProperty<bool> YAxisLogarithmicScaleProperty =
	        AvaloniaProperty.Register<LineChart, bool>(nameof(YAxisLogarithmicScale));

        public static readonly StyledProperty<double> XAxisCurrentValueProperty =
	        AvaloniaProperty.Register<LineChart, double>(nameof(XCurrentValue));

        public static readonly StyledProperty<double> XAxisMinValueProperty =
	        AvaloniaProperty.Register<LineChart, double>(nameof(XMinValue));

        public static readonly StyledProperty<double> XAxisMaxValueProperty =
	        AvaloniaProperty.Register<LineChart, double>(nameof(XMaxValue));

        public static readonly StyledProperty<IBrush?> YAxisStrokeProperty =
            AvaloniaProperty.Register<LineChart, IBrush?>(nameof(YAxisStroke));

        public static readonly StyledProperty<double> YAxisStrokeThicknessProperty =
            AvaloniaProperty.Register<LineChart, double>(nameof(YAxisStrokeThickness));

        public static readonly StyledProperty<double> YAxisArrowSizeProperty =
            AvaloniaProperty.Register<LineChart, double>(nameof(YAxisArrowSize));

        #endregion

        #region YAxis Title

        public static readonly StyledProperty<string> YAxisTitleProperty =
	        AvaloniaProperty.Register<LineChart, string>(nameof(YAxisTitle));

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

        public static readonly StyledProperty<double> CursorStrokeThicknessProperty =
            AvaloniaProperty.Register<LineChart, double>(nameof(CursorStrokeThickness), 1.0);

        public static readonly StyledProperty<IDashStyle?> CursorStrokeDashStyleProperty =
	        AvaloniaProperty.Register<LineChart, IDashStyle?>(nameof(CursorStrokeDashStyle), null);

        public static readonly StyledProperty<PenLineCap> CursorStrokeLineCapProperty =
	        AvaloniaProperty.Register<LineChart, PenLineCap>(nameof(CursorStrokeLineCap), PenLineCap.Flat);

        public static readonly StyledProperty<PenLineJoin> CursorStrokeLineJoinProperty =
	        AvaloniaProperty.Register<LineChart, PenLineJoin>(nameof(CursorStrokeLineJoin), PenLineJoin.Miter);

        public static readonly StyledProperty<double> CursorStrokeMiterLimitProperty =
	        AvaloniaProperty.Register<LineChart, double>(nameof(CursorStrokeMiterLimit), 10.0);

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
	        AffectsMeasure<LineChart>(AreaMarginProperty);
	        AffectsRender<LineChart>(
		        YAxisValuesProperty,
		        XAxisLabelsProperty,
		        XAxisMinValueProperty,
		        XAxisMaxValueProperty,
		        XAxisCurrentValueProperty);
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

        #region Area

        public Thickness AreaMargin
        {
	        get => GetValue(AreaMarginProperty);
	        set => SetValue(AreaMarginProperty, value);
        }

        public IBrush? AreaFill
        {
            get => GetValue(AreaFillProperty);
            set => SetValue(AreaFillProperty, value);
        }

        public IBrush? AreaStroke
        {
            get => GetValue(AreaStrokeProperty);
            set => SetValue(AreaStrokeProperty, value);
        }

        public double AreaStrokeThickness
        {
            get => GetValue(AreaStrokeThicknessProperty);
            set => SetValue(AreaStrokeThicknessProperty, value);
        }

        public IDashStyle? AreaStrokeDashStyle
        {
	        get => GetValue(AreaStrokeDashStyleProperty);
	        set => SetValue(AreaStrokeDashStyleProperty, value);
        }

        public PenLineCap AreaStrokeLineCap
        {
	        get => GetValue(AreaStrokeLineCapProperty);
	        set => SetValue(AreaStrokeLineCapProperty, value);
        }

        public PenLineJoin AreaStrokeLineJoin
        {
	        get => GetValue(AreaStrokeLineJoinProperty);
	        set => SetValue(AreaStrokeLineJoinProperty, value);
        }

        public double AreaStrokeMiterLimit
        {
	        get => GetValue(AreaStrokeMiterLimitProperty);
	        set => SetValue(AreaStrokeMiterLimitProperty, value);
        }

        #endregion

        #region XAxis

        public double XCurrentValue
        {
	        get => GetValue(XAxisCurrentValueProperty);
	        set => SetValue(XAxisCurrentValueProperty, value);
        }

        public double XMinValue
        {
	        get => GetValue(XAxisMinValueProperty);
	        set => SetValue(XAxisMinValueProperty, value);
        }

        public double XMaxValue
        {
	        get => GetValue(XAxisMaxValueProperty);
	        set => SetValue(XAxisMaxValueProperty, value);
        }

        public List<string>? XAxisLabels
        {
	        get => GetValue(XAxisLabelsProperty);
	        set => SetValue(XAxisLabelsProperty, value);
        }

        public List<double>? XAxisValues
        {
	        get => GetValue(XAxisValuesProperty);
	        set => SetValue(XAxisValuesProperty, value);
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

        #region XAxis Label

        public IBrush? XAxisLabelForeground
        {
            get => GetValue(XAxisLabelForegroundProperty);
            set => SetValue(XAxisLabelForegroundProperty, value);
        }

        public double XAxisLabelOpacity
        {
	        get => GetValue(XAxisLabelOpacityProperty);
	        set => SetValue(XAxisLabelOpacityProperty, value);
        }

        public double XAxisLabelAngle
        {
            get => GetValue(XAxisLabelAngleProperty);
            set => SetValue(XAxisLabelAngleProperty, value);
        }

        public Point XAxisLabelOffset
        {
            get => GetValue(XAxisLabelOffsetProperty);
            set => SetValue(XAxisLabelOffsetProperty, value);
        }

        public Size XAxisLabelSize
        {
            get => GetValue(XAxisLabelSizeProperty);
            set => SetValue(XAxisLabelSizeProperty, value);
        }

        public TextAlignment XAxisLabelAlignment
        {
            get => GetValue(XAxisLabelAlignmentProperty);
            set => SetValue(XAxisLabelAlignmentProperty, value);
        }

        public FontFamily XAxisLabelFontFamily
        {
            get => GetValue(XAxisLabelFontFamilyProperty);
            set => SetValue(XAxisLabelFontFamilyProperty, value);
        }

        public FontStyle XAxisLabelFontStyle
        {
            get => GetValue(XAxisLabelFontStyleProperty);
            set => SetValue(XAxisLabelFontStyleProperty, value);
        }

        public FontWeight XAxisLabelFontWeight
        {
            get => GetValue(XAxisLabelFontWeightProperty);
            set => SetValue(XAxisLabelFontWeightProperty, value);
        }

        public double XAxisLabelFontSize
        {
            get => GetValue(XAxisLabelFontSizeProperty);
            set => SetValue(XAxisLabelFontSizeProperty, value);
        }

        #endregion

        #region XAxis Title

        public string XAxisTitle
        {
	        get => GetValue(XAxisTitleProperty);
	        set => SetValue(XAxisTitleProperty, value);
        }

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

        #region YAxis

        public List<double>? YValues
        {
	        get => GetValue(YAxisValuesProperty);
	        set => SetValue(YAxisValuesProperty, value);
        }

        public bool YAxisLogarithmicScale
        {
	        get => GetValue(YAxisLogarithmicScaleProperty);
	        set => SetValue(YAxisLogarithmicScaleProperty, value);
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

        #region YAxisTitle

        public string YAxisTitle
        {
	        get => GetValue(YAxisTitleProperty);
	        set => SetValue(YAxisTitleProperty, value);
        }

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

        public double CursorStrokeThickness
        {
	        get => GetValue(CursorStrokeThicknessProperty);
	        set => SetValue(CursorStrokeThicknessProperty, value);
        }

        public IDashStyle? CursorStrokeDashStyle
        {
	        get => GetValue(CursorStrokeDashStyleProperty);
	        set => SetValue(CursorStrokeDashStyleProperty, value);
        }

        public PenLineCap CursorStrokeLineCap
        {
	        get => GetValue(CursorStrokeLineCapProperty);
	        set => SetValue(CursorStrokeLineCapProperty, value);
        }

        public PenLineJoin CursorStrokeLineJoin
        {
	        get => GetValue(CursorStrokeLineJoinProperty);
	        set => SetValue(CursorStrokeLineJoinProperty, value);
        }

        public double CursorStrokeMiterLimit
        {
	        get => GetValue(CursorStrokeMiterLimitProperty);
	        set => SetValue(CursorStrokeMiterLimitProperty, value);
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
	        var rangeValues = XMaxValue - XMinValue;
	        var rangeArea = Bounds.Width - AreaMargin.Left - AreaMargin.Right;
	        var value = Clamp(x - AreaMargin.Left, 0, rangeArea);
	        XCurrentValue = XMaxValue - rangeValues / rangeArea * value;
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

	        state.ChartWidth = width;
	        state.ChartHeight = height;

	        state.AreaMargin = AreaMargin;
	        state.AreaWidth = width - state.AreaMargin.Left - state.AreaMargin.Right;
	        state.AreaHeight = height - state.AreaMargin.Top - state.AreaMargin.Bottom;

	        var values = YValues;
	        if (values is not null)
	        {
		        var logarithmicScale = YAxisLogarithmicScale;

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

	        var labels = XAxisLabels;
	        if (labels is not null)
	        {
		        state.XLabels = labels.ToList();
	        }

	        var minValue = XMinValue;
	        var maxValue = XMaxValue;
	        var cursorValue = XCurrentValue;
	        state.CursorPosition = ScaleHorizontal(maxValue - cursorValue, maxValue, state.AreaWidth);

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
            var brush = AreaStroke;
            if (brush is null)
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
            var thickness = CursorStrokeThickness;
            var dashStyle = CursorStrokeDashStyle;
            var lineCap = CursorStrokeLineCap;
            var lineJoin = CursorStrokeLineJoin;
            var miterLimit = CursorStrokeMiterLimit;
            var pen = new Pen(brush, thickness, dashStyle, lineCap, lineJoin, miterLimit);
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
            var pen = new Pen(brush, thickness, null, PenLineCap.Round, PenLineJoin.Miter, 10.0);
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

        private void DrawXAxisLabels(DrawingContext context, LineChartState state)
        {
	        if (state.XLabels is null)
	        {
		        return;
	        }
	        var foreground = XAxisLabelForeground;
	        if (foreground is null)
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
            for (var i = 0; i < state.XLabels.Count; i++)
            {
	            var origin = new Point(i * state.Step - size.Width / 2 + state.AreaMargin.Left, originTop);
                var constraint = new Size(size.Width, size.Height);
                var formattedText = CreateFormattedText(state.XLabels[i], typeface, alignment, fontSize, constraint);
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

        private void DrawYAxis(DrawingContext context, LineChartState state)
        {
	        var brush = YAxisStroke;
	        if (brush is null)
	        {
		        return;
	        }
            var size = YAxisArrowSize;
            var thickness = YAxisStrokeThickness;
            var pen = new Pen(brush, thickness, null, PenLineCap.Round, PenLineJoin.Miter, 10.0);
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

        private void DrawBorder(DrawingContext context, LineChartState state)
        {
            var brush = BorderBrush;
            if (brush is null)
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
	        public List<string>? XLabels { get; set; }
	        public double Step { get; set; }
	        public double CursorPosition { get; set; }
        }
    }
}