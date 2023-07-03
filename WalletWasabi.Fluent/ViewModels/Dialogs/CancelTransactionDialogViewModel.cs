using System.Linq;
using System.Reactive;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Cancel transaction")]
public partial class CancelTransactionDialogViewModel : DialogViewModelBase<Unit>
{
	public CancelTransactionDialogViewModel(SmartTransaction original, SmartTransaction cancelTransaction)
	{
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		var originalFee = original.Transaction.GetFee(original.WalletInputs.Select(x => x.Coin).Cast<ICoin>().ToArray());
		var cancelFeel = cancelTransaction.Transaction.GetFee(cancelTransaction.WalletInputs.Select(x => x.Coin).Cast<ICoin>().ToArray());
		FeeDifference = originalFee - cancelFeel;

		EnableBack = false;
		NextCommand = ReactiveCommand.Create(
			() =>
			{
				// TODO: Cancel transaction
			});
	}

	public Money FeeDifference { get; }

	protected override void OnDialogClosed()
	{
	}
}
