using System.Collections.Generic;
using DynamicData;
using WalletWasabi.Fluent.Controls;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core.Headers;

internal class SelectionHeaderViewModel : SelectionHeaderViewModelBase
{
	public SelectionHeaderViewModel(
		IObservable<IChangeSet<ISelectable>> changeStream,
		Func<int, string> getContent,
		IEnumerable<CommandViewModel> commands) : base(changeStream, getContent, commands)
	{
	}
}
