using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core;

public interface ISelectable : IReactiveObject
{
	public bool IsSelected { get; set; }
}
