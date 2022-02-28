using System.Collections.Generic;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using ReactiveUI;

namespace WalletWasabi.Fluent.TreeDataGrid;

internal class TreeDataGridPrivacyTextCell : TreeDataGridCell
{
	private static readonly List<TreeDataGridPrivacyTextCell> Realized = new();
	private static IDisposable? Subscription;
	private static bool IsContentVisible = true;
	private string? _value;
	private FormattedText? _formattedText;
	private int _numberOfPrivacyChars;

	public string? Text => IsContentVisible ? _value : new string('#', _value is not null ? _numberOfPrivacyChars : 0);

	public override void Realize(IElementFactory factory, ICell model, int columnIndex, int rowIndex)
	{
		var privacyTextCell = (PrivacyTextCell)model;
		var text = privacyTextCell.Value;

		_numberOfPrivacyChars = privacyTextCell.NumberOfPrivacyChars;

		if (text != _value)
		{
			_value = text;
			_formattedText = null;
		}

		base.Realize(factory, model, columnIndex, rowIndex);
	}

	public override void Render(DrawingContext context)
	{
		if (_formattedText is not null)
		{
			var r = Bounds.CenterRect(_formattedText.Bounds);
			context.DrawText(Foreground, new Point(0, r.Position.Y), _formattedText);
		}
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);

		if (Realized.Count == 0)
		{
			Subscription = Services.UiConfig
				.WhenAnyValue(x => x.PrivacyMode)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => SetContentVisible(!x));
		}

		Realized.Add(this);
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		Realized.Remove(this);

		if (Realized.Count == 0)
		{
			Subscription?.Dispose();
			Subscription = null;
		}
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		if (string.IsNullOrWhiteSpace(Text))
		{
			return default;
		}

		if (availableSize != _formattedText?.Constraint)
		{
			_formattedText = new FormattedText(
				Text,
				new Typeface(FontFamily, FontStyle, FontWeight),
				FontSize,
				TextAlignment.Left,
				TextWrapping.NoWrap,
				availableSize);
		}

		return _formattedText.Bounds.Size;
	}

	private static void SetContentVisible(bool value)
	{
		IsContentVisible = value;

		foreach (var c in Realized)
		{
			c._formattedText = null;
			c.InvalidateMeasure();
		}
	}
}
