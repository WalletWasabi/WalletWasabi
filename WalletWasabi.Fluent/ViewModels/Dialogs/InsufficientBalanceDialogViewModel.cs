using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	public enum InsufficientBalanceUserDecision
	{
		Cancel,
		SendAnyway,
		SelectMoreCoin,
		SubtractTransactionFee
	}

	[NavigationMetaData(Title = "Insufficient Balance")]
	public partial class InsufficientBalanceDialogViewModel : DialogViewModelBase<InsufficientBalanceUserDecision>
	{
		public InsufficientBalanceDialogViewModel()
		{
			Question = "What to do";

			NextCommand = ReactiveCommand.Create<InsufficientBalanceUserDecision>(decision => Close(result: decision));

			SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: false);
		}

		public string Question { get; }
	}
}
