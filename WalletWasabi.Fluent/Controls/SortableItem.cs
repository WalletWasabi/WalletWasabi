using System.Windows.Input;

namespace WalletWasabi.Fluent.Controls;

public record SortableItem(string Name)
{
	public required ICommand SortByDescendingCommand { get; init; }
	public required ICommand SortByAscendingCommand { get; init; }
}
