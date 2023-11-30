using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy;

[NavigationMetaData(Title = "Delete Order", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class ConfirmDeleteOrderViewModel : DialogViewModelBase<bool>
{
	public ConfirmDeleteOrderViewModel(OrderViewModel order)
	{
		Order = order;

		NextCommand = ReactiveCommand.Create(() => Close(result: true));
		CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Cancel));

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	public OrderViewModel Order { get; }
}
