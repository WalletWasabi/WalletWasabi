using System.Collections.Generic;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class TransactionHexCopiedViewModel : RoutableViewModel
{
	public string Caption { get; } = "The transaction hex was copied but not broadcast.\nIf you are not planning to broadcast it yourself then go back or cancel.";
	public override string Title { get; protected set; } = "Transaction Hex Copied!";

	[AutoNotify] private string _nextButtonText;
	[AutoNotify] private string _cancelButtonText;

	private readonly Wallet _wallet;
	private readonly Dictionary<HdPubKey, LabelsArray> _hdPubKeysWithNewLabels;
	private readonly string _oldClipboardContent;
	private readonly string _hex;

	private TransactionHexCopiedViewModel(Wallet wallet,
		Dictionary<WalletWasabi.Blockchain.Keys.HdPubKey, WalletWasabi.Blockchain.Analysis.Clustering.LabelsArray> hdPubKeysWithNewLabels,
		string oldClipboardContent,
		string hex)
	{
		_wallet = wallet;
		_hdPubKeysWithNewLabels = hdPubKeysWithNewLabels;
		_oldClipboardContent = oldClipboardContent;
		_hex = hex;

		NextCommand = ReactiveCommand.Create(OnNext);
		CancelCommand = ReactiveCommand.CreateFromTask(OnCancelAsync);
		BackCommand = ReactiveCommand.CreateFromTask(OnBackAsync);
		_nextButtonText = "Broadcast manually	";
		_cancelButtonText = "Cancel";
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	private async Task RollbackClipboardAsync()
	{
		var currentClipboard = await UiContext.Clipboard.GetTextAsync();
		if (currentClipboard == _hex)
		{
			await UiContext.Clipboard.SetTextAsync(_oldClipboardContent);
		}
	}

	private async Task OnCancelAsync()
	{
		await RollbackClipboardAsync();
		Navigate().Clear();
	}

	private async Task OnBackAsync()
	{
		await RollbackClipboardAsync();
		Navigate().Back();
	}

	private void OnNext()
	{
		_wallet.UpdateUsedHdPubKeysLabels(_hdPubKeysWithNewLabels);
		Navigate().Clear();
	}
}
