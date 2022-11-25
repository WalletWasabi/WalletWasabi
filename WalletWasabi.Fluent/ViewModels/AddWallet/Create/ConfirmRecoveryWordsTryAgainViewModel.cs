using ReactiveUI;
using System.Reactive;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.Create;

[NavigationMetaData(Title = "", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class ConfirmRecoveryWordsTryAgainViewModel : DialogViewModelBase<Unit>
{
	public ConfirmRecoveryWordsTryAgainViewModel()
	{
		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: false);
		NextCommand = ReactiveCommand.Create(() => Close());
	}
}
