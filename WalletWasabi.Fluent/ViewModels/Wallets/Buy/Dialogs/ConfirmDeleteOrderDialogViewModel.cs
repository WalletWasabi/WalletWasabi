using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Dialogs;

[NavigationMetaData(Title = "Delete Order", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class ConfirmDeleteOrderDialogViewModel : DialogViewModelBase<bool>
{
	public ConfirmDeleteOrderDialogViewModel(OrderViewModel order)
	{
		Order = order;

		NextCommand = ReactiveCommand.Create(() => Close(result: true));
		CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Cancel));

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	public OrderViewModel Order { get; }
}
