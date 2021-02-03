﻿using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

namespace WalletWasabi.Fluent.Controls
{
	public partial class LineChart
	{
		// Area

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

		// XAxis

		public static readonly StyledProperty<List<double>?> XAxisValuesProperty =
			AvaloniaProperty.Register<LineChart, List<double>?>(nameof(XAxisValues));

		public static readonly StyledProperty<List<string>?> XAxisLabelsProperty =
			AvaloniaProperty.Register<LineChart, List<string>?>(nameof(XAxisLabels));

		public static readonly StyledProperty<double> XAxisOpacityProperty =
			AvaloniaProperty.Register<LineChart, double>(nameof(XAxisOpacity));

		public static readonly StyledProperty<Point> XAxisOffsetProperty =
			AvaloniaProperty.Register<LineChart, Point>(nameof(XAxisOffset));

		public static readonly StyledProperty<IBrush?> XAxisStrokeProperty =
			AvaloniaProperty.Register<LineChart, IBrush?>(nameof(XAxisStroke));

		public static readonly StyledProperty<double> XAxisStrokeThicknessProperty =
			AvaloniaProperty.Register<LineChart, double>(nameof(XAxisStrokeThickness));

		public static readonly StyledProperty<double> XAxisArrowSizeProperty =
			AvaloniaProperty.Register<LineChart, double>(nameof(XAxisArrowSize));

		// XAxis Label

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

		// XAxis Title

		public static readonly StyledProperty<string> XAxisTitleProperty =
			AvaloniaProperty.Register<LineChart, string>(nameof(XAxisTitle));

		public static readonly StyledProperty<IBrush?> XAxisTitleForegroundProperty =
			AvaloniaProperty.Register<LineChart, IBrush?>(nameof(XAxisTitleForeground));

		public static readonly StyledProperty<double> XAxisTitleOpacityProperty =
			AvaloniaProperty.Register<LineChart, double>(nameof(XAxisTitleOpacity));

		public static readonly StyledProperty<Point> XAxisTitleOffsetProperty =
			AvaloniaProperty.Register<LineChart, Point>(nameof(XAxisTitleOffset));

		public static readonly StyledProperty<Size> XAxisTitleSizeProperty =
			AvaloniaProperty.Register<LineChart, Size>(nameof(XAxisTitleSize));

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

		// YAxis

		public static readonly StyledProperty<List<double>?> YAxisValuesProperty =
			AvaloniaProperty.Register<LineChart, List<double>?>(nameof(YAxisValues));

		public static readonly StyledProperty<bool> YAxisLogarithmicScaleProperty =
			AvaloniaProperty.Register<LineChart, bool>(nameof(YAxisLogarithmicScale));

		public static readonly StyledProperty<double> XAxisCurrentValueProperty =
			AvaloniaProperty.Register<LineChart, double>(nameof(XAxisCurrentValue));

		public static readonly StyledProperty<double> XAxisMinValueProperty =
			AvaloniaProperty.Register<LineChart, double>(nameof(XAxisMinValue));

		public static readonly StyledProperty<double> XAxisMaxValueProperty =
			AvaloniaProperty.Register<LineChart, double>(nameof(XAxisMaxValue));

		public static readonly StyledProperty<double> YAxisOpacityProperty =
			AvaloniaProperty.Register<LineChart, double>(nameof(YAxisOpacity));

		public static readonly StyledProperty<Point> YAxisOffsetProperty =
			AvaloniaProperty.Register<LineChart, Point>(nameof(YAxisOffset));

		public static readonly StyledProperty<IBrush?> YAxisStrokeProperty =
			AvaloniaProperty.Register<LineChart, IBrush?>(nameof(YAxisStroke));

		public static readonly StyledProperty<double> YAxisStrokeThicknessProperty =
			AvaloniaProperty.Register<LineChart, double>(nameof(YAxisStrokeThickness));

		public static readonly StyledProperty<double> YAxisArrowSizeProperty =
			AvaloniaProperty.Register<LineChart, double>(nameof(YAxisArrowSize));

		// YAxis Title

		public static readonly StyledProperty<string> YAxisTitleProperty =
			AvaloniaProperty.Register<LineChart, string>(nameof(YAxisTitle));

		public static readonly StyledProperty<IBrush?> YAxisTitleForegroundProperty =
			AvaloniaProperty.Register<LineChart, IBrush?>(nameof(YAxisTitleForeground));

		public static readonly StyledProperty<double> YAxisTitleOpacityProperty =
			AvaloniaProperty.Register<LineChart, double>(nameof(YAxisTitleOpacity));

		public static readonly StyledProperty<Point> YAxisTitleOffsetProperty =
			AvaloniaProperty.Register<LineChart, Point>(nameof(YAxisTitleOffset));

		public static readonly StyledProperty<Size> YAxisTitleSizeProperty =
			AvaloniaProperty.Register<LineChart, Size>(nameof(YAxisTitleSize));

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

		// Cursor

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

		// Border

		public static readonly StyledProperty<IBrush?> BorderBrushProperty =
			AvaloniaProperty.Register<LineChart, IBrush?>(nameof(BorderBrush));

		public static readonly StyledProperty<double> BorderThicknessProperty =
			AvaloniaProperty.Register<LineChart, double>(nameof(BorderThickness));

		public static readonly StyledProperty<double> BorderRadiusXProperty =
			AvaloniaProperty.Register<LineChart, double>(nameof(BorderRadiusX));

		public static readonly StyledProperty<double> BorderRadiusYProperty =
			AvaloniaProperty.Register<LineChart, double>(nameof(BorderRadiusY));

		// Fields

		private bool _captured;

		// ctor

		static LineChart()
		{
			AffectsMeasure<LineChart>(AreaMarginProperty);
			AffectsRender<LineChart>(
				AreaMarginProperty,
				AreaFillProperty,
				AreaStrokeProperty,
				AreaStrokeThicknessProperty,
				AreaStrokeDashStyleProperty,
				AreaStrokeLineCapProperty,
				AreaStrokeLineJoinProperty,
				AreaStrokeMiterLimitProperty,
				XAxisValuesProperty,
				XAxisLabelsProperty,
				XAxisOpacityProperty,
				XAxisOffsetProperty,
				XAxisStrokeProperty,
				XAxisStrokeThicknessProperty,
				XAxisArrowSizeProperty,
				XAxisLabelForegroundProperty,
				XAxisLabelOpacityProperty,
				XAxisLabelOffsetProperty,
				XAxisLabelSizeProperty,
				XAxisLabelAlignmentProperty,
				XAxisLabelAngleProperty,
				XAxisLabelFontFamilyProperty,
				XAxisLabelFontStyleProperty,
				XAxisLabelFontWeightProperty,
				XAxisLabelFontSizeProperty,
				XAxisTitleProperty,
				XAxisTitleForegroundProperty,
				XAxisTitleOpacityProperty,
				XAxisTitleOffsetProperty,
				XAxisTitleSizeProperty,
				XAxisTitleAlignmentProperty,
				XAxisTitleAngleProperty,
				XAxisTitleFontFamilyProperty,
				XAxisTitleFontStyleProperty,
				XAxisTitleFontWeightProperty,
				XAxisTitleFontSizeProperty,
				YAxisValuesProperty,
				YAxisLogarithmicScaleProperty,
				XAxisCurrentValueProperty,
				XAxisMinValueProperty,
				XAxisMaxValueProperty,
				YAxisOpacityProperty,
				YAxisOffsetProperty,
				YAxisStrokeProperty,
				YAxisStrokeThicknessProperty,
				YAxisArrowSizeProperty,
				YAxisTitleProperty,
				YAxisTitleForegroundProperty,
				YAxisTitleOpacityProperty,
				YAxisTitleOffsetProperty,
				YAxisTitleSizeProperty,
				YAxisTitleAlignmentProperty,
				YAxisTitleAngleProperty,
				YAxisTitleFontFamilyProperty,
				YAxisTitleFontStyleProperty,
				YAxisTitleFontWeightProperty,
				YAxisTitleFontSizeProperty,
				CursorStrokeProperty,
				CursorStrokeThicknessProperty,
				CursorStrokeDashStyleProperty,
				CursorStrokeLineCapProperty,
				CursorStrokeLineJoinProperty,
				CursorStrokeMiterLimitProperty,
				BorderBrushProperty,
				BorderThicknessProperty,
				BorderRadiusXProperty,
				BorderRadiusYProperty);
		}

		// Area

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

		// XAxis

		public double XAxisCurrentValue
		{
			get => GetValue(XAxisCurrentValueProperty);
			set => SetValue(XAxisCurrentValueProperty, value);
		}

		public double XAxisMinValue
		{
			get => GetValue(XAxisMinValueProperty);
			set => SetValue(XAxisMinValueProperty, value);
		}

		public double XAxisMaxValue
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

		public double XAxisOpacity
		{
			get => GetValue(XAxisOpacityProperty);
			set => SetValue(XAxisOpacityProperty, value);
		}

		public Point XAxisOffset
		{
			get => GetValue(XAxisOffsetProperty);
			set => SetValue(XAxisOffsetProperty, value);
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

		// XAxis Label

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

		// XAxis Title

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

		public double XAxisTitleOpacity
		{
			get => GetValue(XAxisTitleOpacityProperty);
			set => SetValue(XAxisTitleOpacityProperty, value);
		}

		public double XAxisTitleAngle
		{
			get => GetValue(XAxisTitleAngleProperty);
			set => SetValue(XAxisTitleAngleProperty, value);
		}

		public Point XAxisTitleOffset
		{
			get => GetValue(XAxisTitleOffsetProperty);
			set => SetValue(XAxisTitleOffsetProperty, value);
		}

		public Size XAxisTitleSize
		{
			get => GetValue(XAxisTitleSizeProperty);
			set => SetValue(XAxisTitleSizeProperty, value);
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

		// YAxis

		public List<double>? YAxisValues
		{
			get => GetValue(YAxisValuesProperty);
			set => SetValue(YAxisValuesProperty, value);
		}

		public bool YAxisLogarithmicScale
		{
			get => GetValue(YAxisLogarithmicScaleProperty);
			set => SetValue(YAxisLogarithmicScaleProperty, value);
		}

		public double YAxisOpacity
		{
			get => GetValue(YAxisOpacityProperty);
			set => SetValue(YAxisOpacityProperty, value);
		}

		public Point YAxisOffset
		{
			get => GetValue(YAxisOffsetProperty);
			set => SetValue(YAxisOffsetProperty, value);
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

		// YAxis Title

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

		public double YAxisTitleOpacity
		{
			get => GetValue(YAxisTitleOpacityProperty);
			set => SetValue(YAxisTitleOpacityProperty, value);
		}

		public double YAxisTitleAngle
		{
			get => GetValue(YAxisTitleAngleProperty);
			set => SetValue(YAxisTitleAngleProperty, value);
		}

		public Point YAxisTitleOffset
		{
			get => GetValue(YAxisTitleOffsetProperty);
			set => SetValue(YAxisTitleOffsetProperty, value);
		}

		public Size YAxisTitleSize
		{
			get => GetValue(YAxisTitleSizeProperty);
			set => SetValue(YAxisTitleSizeProperty, value);
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

		// Cursor

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

		// Border

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
	}
}