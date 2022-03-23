using System.Reactive;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

[NavigationMetaData(Title = "Control your Privacy?")]
public partial class ConfirmLabelsDialogViewModel : DialogViewModelBase<bool>
{
	public ConfirmLabelsDialogViewModel(PocketSuggestionViewModel suggestion)
	{
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: false);

		CancelCommand = ReactiveCommand.Create(() => Close());
		NextCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Normal, true));

		Suggestion = suggestion;
	}

	public PocketSuggestionViewModel Suggestion { get; }
}
