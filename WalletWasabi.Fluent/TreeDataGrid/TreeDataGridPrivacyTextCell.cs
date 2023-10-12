using System.Collections.Generic;
using System.Globalization;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Selection;
using Avalonia.Media;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.TreeDataGrid;

internal class TreeDataGridPrivacyTextCell : TreeDataGridCell
{
	private IDisposable? Subscription;
	private bool IsContentVisible = true;
	private string? _value;
	private FormattedText? _formattedText;
	private int _numberOfPrivacyChars;

	public string? Text => IsContentVisible ? _value : new string('#', _value is not null ? _numberOfPrivacyChars : 0);

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);

		if (change.Property == ForegroundProperty)
		{
			InvalidateVisual();
		}
	}

	public override void Realize(TreeDataGridElementFactory factory, ITreeDataGridSelectionInteraction? selection, ICell model, int columnIndex, int rowIndex)
	{
		var privacyTextCell = (PrivacyTextCell)model;
		var text = privacyTextCell.Value;

		_numberOfPrivacyChars = privacyTextCell.NumberOfPrivacyChars;

		if (text != _value)
		{
			_value = text;
			_formattedText = null;
		}

		base.Realize(factory, selection, model, columnIndex, rowIndex);
	}

	public override void Render(DrawingContext context)
	{
		if (_formattedText is not null)
		{
			var r = Bounds.CenterRect(new Rect(new Point(0, 0), new Size(_formattedText.Width, _formattedText.Height)));
			if (Foreground is { })
			{
				_formattedText.SetForegroundBrush(Foreground);
			}
			context.DrawText(_formattedText, new Point(0, r.Position.Y));
		}
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);

		Subscription = PrivacyModeHelper.DelayedRevealAndHide(
			this.WhenAnyValue(x => x.IsPointerOver),
			Services.UiConfig.WhenAnyValue(x => x.PrivacyMode))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Do(SetContentVisible)
			.Subscribe();
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		Subscription?.Dispose();
		Subscription = null;
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		if (string.IsNullOrWhiteSpace(Text))
		{
			return default;
		}

		if (availableSize.Width != _formattedText?.MaxTextWidth
		    || availableSize.Height != _formattedText?.MaxTextHeight)
		{
			_formattedText = new FormattedText(
				Text,
				CultureInfo.CurrentCulture,
				FlowDirection.LeftToRight,
				new Typeface(FontFamily, FontStyle, FontWeight),
				FontSize,
				null)
			{
				TextAlignment = TextAlignment.Left,
				MaxTextHeight = availableSize.Height,
				MaxTextWidth = availableSize.Width,
				Trimming = TextTrimming.None
			};
		}

		return new Size(_formattedText.Width, _formattedText.Height);
	}

	private void SetContentVisible(bool value)
	{
		IsContentVisible = value;

		_formattedText = null;
		InvalidateMeasure();
		InvalidateVisual();
	}
}
