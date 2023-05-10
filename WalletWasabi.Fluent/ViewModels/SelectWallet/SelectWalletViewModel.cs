using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.ViewModels.SelectWallet;

[NavigationMetaData(Title = "Select Wallet")]
public partial class SelectWalletViewModel : DialogViewModelBase<WalletViewModelBase>
{
	[AutoNotify(SetterModifier = AccessModifier.Private)]
	private ReadOnlyObservableCollection<WalletViewModelBase>? _wallets;

	[AutoNotify(SetterModifier = AccessModifier.Private)]
	private int _columns;
	
	public SelectWalletViewModel()
	{
		SelectCommand = ReactiveCommand.Create<WalletViewModelBase>(wallet =>
		{
			if (wallet is { })
			{
				Close(result: wallet);
			}
		});
	}

	public ICommand SelectCommand { get;}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		UiServices.WalletManager.Wallets
			.ToObservableChangeSet()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Filter(static wallet => wallet.Wallet.KeyManager is { IsWatchOnly: false } or { IsHardwareWallet: true })
			.Bind(out var wallets)
			.Subscribe(_ => Columns = Math.Min(wallets.Count, 4))
			.DisposeWith(disposables);

		Wallets = wallets;
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: false);
	}
}
