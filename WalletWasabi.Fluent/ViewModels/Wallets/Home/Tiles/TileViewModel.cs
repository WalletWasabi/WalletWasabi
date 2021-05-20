using System.Collections.Generic;
using System.Reactive.Disposables;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles
{
	public abstract partial class TileViewModel : ActivatableViewModel
	{
		[AutoNotify] private IList<int>? _columnSpan;
		[AutoNotify] private IList<int>? _rowSpan;
	}
}