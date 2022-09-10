using System.Collections.Generic;
using DynamicData;
using NBitcoin;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core.Headers;

internal class SelectionHeaderViewModel : SelectionHeaderViewModelBase<OutPoint>
{
	public SelectionHeaderViewModel(
		IObservable<IChangeSet<ISelectable, OutPoint>> changeStream,
		Func<int, string> getContent,
		IEnumerable<CommandViewModel> commands) : base(changeStream, getContent, commands)
	{
	}
}
