using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

[NavigationMetaData(Title = "Rename Wallet")]
public partial class WalletRenameViewModel : DialogViewModelBase<Unit>
{
	private readonly WalletViewModelBase _walletViewModelBase;
	[AutoNotify] private string _walletName;

	public WalletRenameViewModel(WalletViewModelBase walletViewModelBase)
	{
		_walletViewModelBase = walletViewModelBase;

		WalletName = _walletViewModelBase.WalletName;

		NextCommand = ReactiveCommand.Create(
			OnNext, canExecute: this.WhenAnyValue(x=>x.WalletName).Select(x=>!string.IsNullOrEmpty(x)));

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	private void OnNext()
	{
		_walletViewModelBase.WalletName = WalletName;
		Close(DialogResultKind.Normal, Unit.Default);
	}
}
