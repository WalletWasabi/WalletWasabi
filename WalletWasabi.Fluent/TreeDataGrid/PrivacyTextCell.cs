using Avalonia.Controls.Models.TreeDataGrid;

namespace WalletWasabi.Fluent.TreeDataGrid;

internal class PrivacyTextCell : ICell
{
	public PrivacyTextCell(string? value, int numberOfPrivacyChars)
	{
		Value = value;
		NumberOfPrivacyChars = numberOfPrivacyChars;
	}

	public bool CanEdit => false;

	public BeginEditGestures EditGestures => BeginEditGestures.None;

	public string? Value { get; }

	public int NumberOfPrivacyChars { get; }

	object? ICell.Value => Value;
}
