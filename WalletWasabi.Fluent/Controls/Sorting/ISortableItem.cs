using System.Windows.Input;

namespace WalletWasabi.Fluent.Controls.Sorting;

public interface ISortableItem
{
	ICommand SortByDescendingCommand { get; }
	ICommand SortByAscendingCommand { get; }
	string Name { get; }
}