using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.TreeDataGrid;

internal class TreeDataGridPrivacyTextCell : TreeDataGridCell
{
	private FormattedText? _formattedText;
	private Rect _bounds = Rect.Empty;
	private bool _isContentVisible = true;
	private string _mask = "";
	private int _numberOfPrivacyChars;
	private IDisposable _subscription = Disposable.Empty;
	private string? _value;

	private string? Text => _isContentVisible ? _value : TextHelpers.GetPrivacyMask(_numberOfPrivacyChars);

	public override void Realize(IElementFactory factory, ICell model, int columnIndex, int rowIndex)
	{
		var privacyTextCell = (PrivacyTextCell) model;
		var text = privacyTextCell.Value;

		_numberOfPrivacyChars = privacyTextCell.NumberOfPrivacyChars;
		_mask = TextHelpers.GetPrivacyMask(_numberOfPrivacyChars);

		if (text != _value)
		{
			_value = text;
			_formattedText = null;
		}

		base.Realize(factory, model, columnIndex, rowIndex);
	}

	public override void Render(DrawingContext context)
	{
		if (_formattedText is null)
		{
			var placeHolder = CreatePlaceHolder(_mask);
			var bounds = new Rect(0, 0, placeHolder.Width, placeHolder.Height);
			var rc = Bounds.CenterRect(bounds);
			context.DrawText(placeHolder, new Point(0, rc.Position.Y));
			return;
		}

		var r = Bounds.CenterRect(_bounds);
		context.DrawText(_formattedText, new Point(0, r.Position.Y));
	}


	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		var displayContent = PrivacyModeHelper.DelayedRevealAndHide(
			this.WhenAnyValue(x => x.IsPointerOver),
			Services.UiConfig.WhenAnyValue(x => x.PrivacyMode));

		_subscription = displayContent
			.ObserveOn(RxApp.MainThreadScheduler)
			.Do(SetContentVisible)
			.Subscribe();
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		_subscription.Dispose();
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		if (string.IsNullOrWhiteSpace(Text))
		{
			return default;
		}

		if (_formattedText is null || availableSize != _bounds.Size)
		{
			InvalidateFormattedText(Text);
		}

		return _bounds.Size;
	}

	private FormattedText CreatePlaceHolder(string mask)
	{
		var placeHolder = new FormattedText(
			mask,
			CultureInfo.CurrentCulture,
			FlowDirection.LeftToRight,
			new Typeface(FontFamily, FontStyle, FontWeight),
			FontSize,
			Brushes.Transparent);

		placeHolder.TextAlignment = TextAlignment.Left;
		placeHolder.Trimming = TextTrimming.None;

		return placeHolder;
	}

	private void InvalidateFormattedText(string text)
	{
		_formattedText = new FormattedText(
			text,
			CultureInfo.CurrentCulture,
			FlowDirection.LeftToRight,
			new Typeface(FontFamily, FontStyle, FontWeight),
			FontSize,
			Foreground);

		_formattedText.TextAlignment = TextAlignment.Left;
		_formattedText.Trimming = TextTrimming.None;

		_bounds = new Rect(0, 0, _formattedText.Width, _formattedText.Height);
	}

	private void SetContentVisible(bool value)
	{
		_isContentVisible = value;
		_formattedText = null;
		InvalidateMeasure();
	}
}
