using System.Windows.Input;

namespace WalletWasabi.Fluent.Controls.Sorting;

public record SortableItem(string Name) : ISortableItem
{
	public required ICommand SortByDescendingCommand { get; init; }
	public required ICommand SortByAscendingCommand { get; init; }
}
