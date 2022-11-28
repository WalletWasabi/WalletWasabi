using System.Collections.Generic;
using System.ComponentModel;

namespace WalletWasabi.Fluent.ViewModels.CoinControl.Core;

public interface IHierarchicallySelectable : INotifyPropertyChanged
{
	IEnumerable<IHierarchicallySelectable> Selectables { get; }
	public bool? IsSelected { get; set; }
}
