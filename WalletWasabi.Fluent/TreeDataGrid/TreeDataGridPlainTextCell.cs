using System.Globalization;
using Avalonia;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Selection;
using Avalonia.Media;

namespace WalletWasabi.Fluent.TreeDataGrid;

internal class TreeDataGridPlainTextCell : TreeDataGridCell
{
	private FormattedText? _formattedText;
	private string? _value;

	public string? Text => _value;

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
		var plainTextCell = (PlainTextCell)model;
		var text = plainTextCell.Value;

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
			var r = Bounds.CenterRect(new Rect(new Size(_formattedText.Width, _formattedText.Height)));
			if (Foreground is { })
			{
				_formattedText.SetForegroundBrush(Foreground);
			}
			context.DrawText(_formattedText, new Point(0, r.Position.Y));
		}
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		if (string.IsNullOrWhiteSpace(Text))
		{
			return default;
		}

		if (availableSize.Width != _formattedText?.MaxTextWidth
		    || availableSize.Height != _formattedText.MaxTextHeight)
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
			_formattedText.Trimming = TextTrimming.None;
		}

		return new Size(_formattedText.Width, _formattedText.Height);
	}
}
