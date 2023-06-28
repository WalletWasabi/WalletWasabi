using System.Reactive;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Boost transaction")]
public partial class BoostTransactionDialogViewModel : DialogViewModelBase<Unit>
{
	public BoostTransactionDialogViewModel()
	{
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;
	}

	protected override void OnDialogClosed()
	{
	}
}
