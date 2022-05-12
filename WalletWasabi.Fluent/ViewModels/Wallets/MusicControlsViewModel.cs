using ReactiveUI;
using System.Reactive.Disposables;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public partial class MusicControlsViewModel : ViewModelBase
{
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isActive;
	[AutoNotify] private WalletViewModel? _currentWallet;

	public MusicControlsViewModel()
	{
		this.WhenAnyValue(x => x.CurrentWallet.IsWalletBalanceZero)
			.Subscribe(x => SetIsActive(CurrentWallet));
	}

	public IDisposable SetWallet(WalletViewModel wallet)
	{
		CurrentWallet = wallet;

		SetIsActive(wallet);

		return new CompositeDisposable(
			Disposable.Create(() => IsActive = false));
	}

	private void SetIsActive(WalletViewModel wallet)
	{
		IsActive = !(wallet.Wallet.KeyManager.IsWatchOnly || wallet.Wallet.KeyManager.IsHardwareWallet || wallet.IsWalletBalanceZero);
	}
}
