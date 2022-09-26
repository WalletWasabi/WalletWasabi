using System.Collections.ObjectModel;
using WalletWasabi.Fluent.Controls;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core.Cells;

public class SelectableCollectionCellViewModel : ViewModelBase
{
	public SelectableCollectionCellViewModel(ReadOnlyObservableCollection<ISelectable> selectables)
	{
		Selectables = selectables;
	}

	public ReadOnlyObservableCollection<ISelectable> Selectables { get; }
}
