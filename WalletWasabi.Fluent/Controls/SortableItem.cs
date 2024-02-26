using System.Windows.Input;

namespace WalletWasabi.Fluent.Controls;

public interface ISortableItem
{
	ICommand SortByDescendingCommand { get; }
	ICommand SortByAscendingCommand { get; }
	string Name { get; }
}

public class SortableItemDesign : ISortableItem
{
	public ICommand SortByDescendingCommand { get; set; }
	public ICommand SortByAscendingCommand { get; set; }
	public string Name { get; set; }
}

public record SortableItem(string Name) : ISortableItem
{
	public required ICommand SortByDescendingCommand { get; init; }
	public required ICommand SortByAscendingCommand { get; init; }
}
