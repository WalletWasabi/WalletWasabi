using System.Reactive;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

[NavigationMetaData(Title = "Control your Privacy?")]
public class ConfirmLabelsDialogViewModel : DialogViewModelBase<bool>
{
	public ConfirmLabelsDialogViewModel(PocketSuggestionViewModel suggestion)
	{
		SetupCancel(true, false, false);

		NextCommand = ReactiveCommand.Create(() => Close());
		CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Normal, true));

		Suggestion = suggestion;
	}

	public PocketSuggestionViewModel Suggestion { get; }
}