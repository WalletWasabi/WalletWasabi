using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Hide Address", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class ConfirmHideAddressViewModel : DialogViewModelBase<bool>
{
	public ConfirmHideAddressViewModel(LabelsArray labels)
	{
		Labels = labels;

		NextCommand = ReactiveCommand.Create(() => Close(result: true));
		CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Cancel));

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	public LabelsArray Labels { get; }
}
