using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WalletWasabi.Fluent.Mobile.Controls;

/// <summary>Resolution-independent reference iconography. No font glyph or bitmap dependency.</summary>
public sealed class MobileGlyph : Control
{
	public static readonly StyledProperty<string> KindProperty = AvaloniaProperty.Register<MobileGlyph, string>(nameof(Kind), "shield");
	public static readonly StyledProperty<IBrush?> ForegroundProperty = AvaloniaProperty.Register<MobileGlyph, IBrush?>(nameof(Foreground), Brushes.Black);
	private static readonly IReadOnlyDictionary<string, Geometry> Shapes = new Dictionary<string, Geometry>(StringComparer.Ordinal)
	{
		["home"] = Geometry.Parse("M3,11 L12,3 21,11 M5,10 L5,21 10,21 10,15 14,15 14,21 19,21 19,10"),
		["send"] = Geometry.Parse("M12,21 L12,3 M5,10 L12,3 19,10"),
		["receive"] = Geometry.Parse("M12,3 L12,21 M5,14 L12,21 19,14"),
		["back"] = Geometry.Parse("M15,4 L7,12 15,20"),
		["next"] = Geometry.Parse("M9,4 L17,12 9,20"),
		["shield"] = Geometry.Parse("M12,2 L21,6 20,14 Q18,20 12,23 Q6,20 4,14 L3,6 Z M8,12 L11,15 16,9"),
		["coinjoin"] = Geometry.Parse("M4,9 A8,8 0 0 1 18,6 L21,9 M21,4 L21,9 16,9 M20,15 A8,8 0 0 1 6,18 L3,15 M3,20 L3,15 8,15"),
		["history"] = Geometry.Parse("M4,5 L6,5 M10,5 L21,5 M4,12 L6,12 M10,12 L21,12 M4,19 L6,19 M10,19 L21,19"),
		["settings"] = Geometry.Parse("M4,6 L20,6 M4,12 L20,12 M4,18 L20,18 M8,3 L8,9 M16,9 L16,15 M10,15 L10,21"),
		["copy"] = Geometry.Parse("M8,8 L21,8 21,21 8,21 Z M16,4 L16,2 2,2 2,16 4,16"),
		["qr"] = Geometry.Parse("M2,2 L9,2 9,9 2,9 Z M15,2 L22,2 22,9 15,9 Z M2,15 L9,15 9,22 2,22 Z M15,15 L18,15 18,18 22,18 22,22 15,22 Z"),
		["eye"] = Geometry.Parse("M2,12 Q12,-2 22,12 Q12,26 2,12 Z M15,12 A3,3 0 1 1 9,12 A3,3 0 1 1 15,12"),
		["coins"] = Geometry.Parse("M4,6 Q4,2 12,2 Q20,2 20,6 Q20,10 12,10 Q4,10 4,6 Z M4,6 L4,12 Q4,16 12,16 Q20,16 20,12 L20,6 M4,12 L4,18 Q4,22 12,22 Q20,22 20,18 L20,12"),
		["discover"] = Geometry.Parse("M21,11 A9,9 0 1 1 3,11 A9,9 0 1 1 21,11 M8,15 L11,8 17,6 14,13 Z"),
		["leaf"] = Geometry.Parse("M12,22 Q1,17 4,5 Q12,9 12,22 M12,22 Q11,8 21,2 Q25,16 12,22 M12,22 L19,7"),
		["check"] = Geometry.Parse("M4,12 L9,18 21,5"),
		["close"] = Geometry.Parse("M5,5 L19,19 M19,5 L5,19"),
		["lock"] = Geometry.Parse("M5,10 L19,10 19,22 5,22 Z M8,10 L8,6 A4,4 0 0 1 16,6 L16,10 M12,14 L12,18"),
		["external"] = Geometry.Parse("M14,3 L22,3 22,11 M22,3 L11,14 M10,4 L3,4 3,21 20,21 20,14"),
		["pause"] = Geometry.Parse("M7,4 L7,20 M17,4 L17,20")
	};
	static MobileGlyph() => AffectsRender<MobileGlyph>(KindProperty, ForegroundProperty);
	public string Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
	public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
	public override void Render(DrawingContext context)
	{
		if (!Shapes.TryGetValue(Kind, out var geometry) || Bounds.Width <= 0 || Bounds.Height <= 0) return;
		var size = Math.Min(Bounds.Width, Bounds.Height);
		using (context.PushTransform(Matrix.CreateScale(size / 26, size / 26) * Matrix.CreateTranslation((Bounds.Width - size) / 2 + size / 26, (Bounds.Height - size) / 2 + size / 26)))
			context.DrawGeometry(null, new Pen(Foreground, 1.65, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geometry);
	}
	protected override Size MeasureOverride(Size availableSize) => new(24, 24);
}

/// <summary>Percentage of balance meeting the anonymity target; not a probability of anonymity.</summary>
public sealed class MobilePrivacyRing : Control
{
	public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<MobilePrivacyRing, double>(nameof(Value));
	public static readonly StyledProperty<IBrush?> ForegroundProperty = AvaloniaProperty.Register<MobilePrivacyRing, IBrush?>(nameof(Foreground));
	public static readonly StyledProperty<IBrush?> TrackProperty = AvaloniaProperty.Register<MobilePrivacyRing, IBrush?>(nameof(Track));
	public static readonly StyledProperty<double> StrokeThicknessProperty = AvaloniaProperty.Register<MobilePrivacyRing, double>(nameof(StrokeThickness), 10);
	static MobilePrivacyRing() => AffectsRender<MobilePrivacyRing>(ValueProperty, ForegroundProperty, TrackProperty, StrokeThicknessProperty);
	public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
	public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
	public IBrush? Track { get => GetValue(TrackProperty); set => SetValue(TrackProperty, value); }
	public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
	public override void Render(DrawingContext context)
	{
		var thickness = double.IsFinite(StrokeThickness) ? Math.Max(1, StrokeThickness) : 10;
		var radius = (Math.Min(Bounds.Width, Bounds.Height) - thickness) / 2;
		if (radius <= 0) return;
		var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
		context.DrawEllipse(null, new Pen(Track, thickness), center, radius, radius);
		var fraction = double.IsFinite(Value) ? Math.Clamp(Value, 0, 100) / 100 : 0;
		if (fraction <= 0) return;
		var geometry = new StreamGeometry();
		using (var g = geometry.Open())
		{
			g.BeginFigure(new Point(center.X, center.Y - radius), false);
			var count = Math.Max(1, (int)Math.Ceiling(fraction * 180));
			for (var i = 1; i <= count; i++)
			{
				var angle = fraction * Math.PI * 2 * i / count - Math.PI / 2;
				g.LineTo(new Point(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius));
			}
			g.EndFigure(false);
		}
		context.DrawGeometry(null, new Pen(Foreground, thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geometry);
	}
}

/// <summary>Uses supplied historical values; never fabricates prices or balances.</summary>
public sealed class MobileSparkline : Control
{
	public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty = AvaloniaProperty.Register<MobileSparkline, IReadOnlyList<double>?>(nameof(Values));
	public static readonly StyledProperty<IBrush?> ForegroundProperty = AvaloniaProperty.Register<MobileSparkline, IBrush?>(nameof(Foreground));
	static MobileSparkline() => AffectsRender<MobileSparkline>(ValuesProperty, ForegroundProperty);
	public IReadOnlyList<double>? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
	public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
	public override void Render(DrawingContext context)
	{
		if (Values is not { Count: > 1 } values || Bounds.Width <= 4 || Bounds.Height <= 4) return;
		var min = double.PositiveInfinity;
		var max = double.NegativeInfinity;
		foreach (var value in values)
		{
			if (!double.IsFinite(value)) return;
			min = Math.Min(min, value); max = Math.Max(max, value);
		}
		var range = max - min;
		if (!double.IsFinite(range)) return;
		var geometry = new StreamGeometry();
		using (var g = geometry.Open())
		{
			for (var i = 0; i < values.Count; i++)
			{
				var point = new Point(2 + (Bounds.Width - 4) * i / (values.Count - 1), range <= 0 ? Bounds.Height / 2 : 2 + (Bounds.Height - 4) * (1 - (values[i] - min) / range));
				if (i == 0) g.BeginFigure(point, false); else g.LineTo(point);
			}
			g.EndFigure(false);
		}
		context.DrawGeometry(null, new Pen(Foreground, 1.6, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geometry);
	}
}

/// <summary>Generator [x,y] matrix with four quiet modules and device-pixel aligned cells.</summary>
public sealed class MobileQrCode : Control
{
	public static readonly StyledProperty<bool[,]?> MatrixProperty = AvaloniaProperty.Register<MobileQrCode, bool[,]?>(nameof(Matrix));
	static MobileQrCode() => AffectsRender<MobileQrCode>(MatrixProperty);
	public bool[,]? Matrix { get => GetValue(MatrixProperty); set => SetValue(MatrixProperty, value); }
	public override void Render(DrawingContext context)
	{
		context.DrawRectangle(Brushes.White, null, new Rect(Bounds.Size));
		if (Matrix is not { } matrix || matrix.GetLength(0) == 0 || matrix.GetLength(0) != matrix.GetLength(1)) return;
		var modules = matrix.GetLength(0);
		var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
		var cellPixels = Math.Floor(Math.Min(Bounds.Width, Bounds.Height) * scale / (modules + 8));
		if (cellPixels < 1) return;
		var cell = cellPixels / scale;
		var originX = Math.Floor((Bounds.Width * scale - (modules + 8) * cellPixels) / 2) / scale + 4 * cell;
		var originY = Math.Floor((Bounds.Height * scale - (modules + 8) * cellPixels) / 2) / scale + 4 * cell;
		for (var x = 0; x < modules; x++)
			for (var y = 0; y < modules; y++)
				if (matrix[x, y]) context.DrawRectangle(Brushes.Black, null, new Rect(originX + x * cell, originY + y * cell, cell, cell));
	}
}
