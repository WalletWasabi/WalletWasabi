using Avalonia.Controls.Models.TreeDataGrid;

namespace WalletWasabi.Fluent.TreeDataGrid;

internal class PrivacyTextCell : ICell
{
	public PrivacyTextCell(string? value, PrivacyCellType type, int numberOfPrivacyChars, bool ignorePrivacyMode = false)
	{
		Value = value;
		Type = type;
		NumberOfPrivacyChars = numberOfPrivacyChars;
		IgnorePrivacyMode = ignorePrivacyMode;
	}

	public bool CanEdit => false;

	public BeginEditGestures EditGestures => BeginEditGestures.None;

	public string? Value { get; }

	public PrivacyCellType Type { get; }

	public int NumberOfPrivacyChars { get; }

	public bool IgnorePrivacyMode { get; }

	object? ICell.Value => Value;
}
