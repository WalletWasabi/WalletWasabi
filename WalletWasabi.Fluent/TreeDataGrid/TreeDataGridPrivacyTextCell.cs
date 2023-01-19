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
	private bool _isContentVisible = true;
	private string _mask = "";
	private int _numberOfPrivacyChars;
	private IDisposable _subscription = Disposable.Empty;
	private string? _value;

	private string? Text => _isContentVisible ? _value : TextHelpers.GetPrivacyMask(_numberOfPrivacyChars);

	public override void Realize(IElementFactory factory, ICell model, int columnIndex, int rowIndex)
	{
		var privacyTextCell = (PrivacyTextCell)model;
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
		if (Background is { } background)
		{
			context.FillRectangle(background, new Rect(Bounds.Size));
		}

		if (_formattedText is null)
		{
			var placeHolder = new FormattedText(
				_mask,
				new Typeface(FontFamily, FontStyle, FontWeight),
				FontSize,
				TextAlignment.Left,
				TextWrapping.NoWrap,
				Size.Infinity);

			var rc = Bounds.CenterRect(placeHolder.Bounds);
			context.DrawText(Brushes.Transparent, new Point(0, rc.Position.Y), placeHolder);
			return;
		}

		var r = Bounds.CenterRect(_formattedText.Bounds);
		context.DrawText(Foreground, new Point(0, r.Position.Y), _formattedText);
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

	private void SetContentVisible(bool value)
	{
		_isContentVisible = value;
		_formattedText = null;
		InvalidateMeasure();
	}
}
