using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace WalletWasabi.Fluent.TreeDataGrid;

internal class TreeDataGridPlainTextCell : TreeDataGridCell
{
	private FormattedText? _formattedText;
	private string? _value;

	private string? Text => _value;

	public override void Realize(IElementFactory factory, ICell model, int columnIndex, int rowIndex)
	{
		var plainTextCell = (PlainTextCell)model;
		var text = plainTextCell.Value;

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
}
