using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace WalletWasabi.Fluent.TreeDataGrid;

internal class TreeDataGridPlainTextCell : TreeDataGridCell
{
	private FormattedText? _formattedText;
	private string? _text;

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);

		if (change.Property == ForegroundProperty)
		{
			InvalidateVisual();
		}
	}

	public override void Realize(TreeDataGridElementFactory factory, ICell model, int columnIndex, int rowIndex)
	{
		var plainTextCell = (PlainTextCell)model;
		var text = plainTextCell.Value;

		if (text != _text)
		{
			_text = text;
			_formattedText = null;
		}

		base.Realize(factory, model, columnIndex, rowIndex);
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
		if (string.IsNullOrWhiteSpace(_text))
		{
			return default;
		}

		if (availableSize.Width != _formattedText.Width || availableSize.Height != _formattedText.Height)
		{
			_formattedText = new FormattedText(
				_text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
				new Typeface(FontFamily, FontStyle, FontWeight),
				FontSize, null)
			{
				TextAlignment =
					TextAlignment.Left,
				MaxTextHeight = availableSize.Height, MaxTextWidth = availableSize.Width
			};
		}

		return new Size(_formattedText.Width, _formattedText.Height);
	}
}
