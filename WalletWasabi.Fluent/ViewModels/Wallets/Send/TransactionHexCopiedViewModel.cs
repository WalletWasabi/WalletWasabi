using System.Collections.Generic;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class TransactionHexCopiedViewModel : RoutableViewModel
{
	public string Caption { get; } = "The transaction hex was copied but not broadcast.";
	public override string Title { get; protected set; } = "Transaction Hex Copied!";

	[AutoNotify] private string _nextButtonText;
	[AutoNotify] private string _cancelButtonText;

	private readonly Wallet _wallet;
	private readonly Dictionary<HdPubKey, LabelsArray> _hdPubKeysWithNewLabels;



	private TransactionHexCopiedViewModel(Wallet wallet,
		Dictionary<WalletWasabi.Blockchain.Keys.HdPubKey, WalletWasabi.Blockchain.Analysis.Clustering.LabelsArray> hdPubKeysWithNewLabels)
	{
		_wallet = wallet;
		_hdPubKeysWithNewLabels = hdPubKeysWithNewLabels;

		NextCommand = ReactiveCommand.Create(OnNext);
		_nextButtonText = "Will broadcast";
		_cancelButtonText = "Won't broadcast";
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	private void OnNext()
	{
		_wallet.UpdateUsedHdPubKeysLabels(_hdPubKeysWithNewLabels);
		Navigate().Clear();
	}
}
