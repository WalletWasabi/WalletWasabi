using System.Reactive.Disposables;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public partial class MusicControlsViewModel : ViewModelBase
{
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isActive;
	[AutoNotify] private WalletViewModel? _currentWallet;

	public IDisposable SetWallet(WalletViewModel wallet)
	{
		CurrentWallet = wallet;

		IsActive = true;

		return new CompositeDisposable(
			Disposable.Create(() => IsActive = false));
	}
}