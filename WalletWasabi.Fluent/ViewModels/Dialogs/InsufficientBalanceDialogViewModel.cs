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
		public InsufficientBalanceDialogViewModel(bool enableSendAnyway, bool enableSelectMoreCoin, bool enableSubtractFee)
		{
			EnableSendAnyway = enableSendAnyway;
			EnableSelectMoreCoin = enableSelectMoreCoin;
			EnableSubtractFee = enableSubtractFee;
			Question = "What to do";

			NextCommand = ReactiveCommand.Create<InsufficientBalanceUserDecision>(decision => Close(result: decision));

			SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: false);
		}

		public bool EnableSendAnyway { get; }

		public bool EnableSelectMoreCoin { get; }

		public bool EnableSubtractFee { get; }

		public string Question { get; }
	}
}
