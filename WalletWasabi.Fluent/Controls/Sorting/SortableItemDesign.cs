using System.Windows.Input;

namespace WalletWasabi.Fluent.Controls.Sorting;

public class SortableItemDesign : ISortableItem
{
	public ICommand SortByDescendingCommand { get; set; } = null!;
	public ICommand SortByAscendingCommand { get; set; } = null!;
	public string Name { get; set; } = null!;
	public bool IsDescendingActive { get; set; }
	public bool IsAscendingActive { get; set; }
}
