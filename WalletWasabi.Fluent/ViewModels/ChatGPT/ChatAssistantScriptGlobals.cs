using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.ChatGPT.Messages;
using WalletWasabi.Fluent.ViewModels.ChatGPT.Messages.Actions;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.ViewModels.ChatGPT;

public class ChatAssistantScriptGlobals
{
	public ChatAssistantViewModel Chat { get; set; }

	public MainViewModel Main { get; set; }

	public async Task<string> Send(string address, string amount, string[] labels)
	{
		var resultMessage = $"Sending {amount} to {address}, {labels}...";

		Chat.Messages.Add(new SendActionMessageViewModel()
		{
			Message = resultMessage
		});

		// TODO:

		return resultMessage;
	}

	public async Task<string> Receive(string[] labels)
	{
		// TODO: Show receive dialog but QR cod progress view.

		var resultMessage = "";
		var address = default(string);

		if (MainViewModel.Instance.MainScreen.CurrentPage is WalletViewModel currentWallet)
		{
			var newKey = currentWallet.Wallet.KeyManager.GetNextReceiveKey(new SmartLabel(labels));
			address = newKey.GetP2wpkhAddress( currentWallet.Wallet.Network).ToString();
			resultMessage = $"New receive address: {address}";
		}
		else
		{
			resultMessage = "Can't generate receive address, please login into wallet.";
		}

		Chat.Messages.Add(new ReceiveActionMessageViewModel()
		{
			Message = resultMessage,
			// TODO: Enable copy new address (use copy button).
			Address = address
		});

		return resultMessage;
	}

	public async Task<string> Balance()
	{
		// TODO:

		var resultMessage = "";
		var balance = default(string);

		if (MainViewModel.Instance.MainScreen.CurrentPage is WalletViewModel currentWallet)
		{
			balance = currentWallet.Wallet.Coins.TotalAmount().ToFormattedString();
			resultMessage = $"{currentWallet.Wallet.WalletName} balance is {balance}";
		}
		else
		{
			resultMessage = "Can't provide wallet balance, please login into wallet.";
		}

		Chat.Messages.Add(new BalanceActionMessageViewModel()
		{
			Message = resultMessage,
			Balance = balance
		});

		return resultMessage;
	}
}
