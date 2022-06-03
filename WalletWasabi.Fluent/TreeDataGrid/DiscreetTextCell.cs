using Avalonia.Controls.Models.TreeDataGrid;

namespace WalletWasabi.Fluent.TreeDataGrid;

internal class DiscreetTextCell : ICell
{
	public DiscreetTextCell(string? value, int numberOfDiscreetChars)
	{
		Value = value;
		NumberOfDiscreetChars = numberOfDiscreetChars;
	}

	public bool CanEdit => false;

	public string? Value { get; }

	public int NumberOfDiscreetChars { get; }

	object? ICell.Value => Value;
}
