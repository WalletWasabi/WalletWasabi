using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.ChatGPT;

public class ChatAssistantScriptGlobals
{
	public ChatAssistantViewModel Chat { get; set; }

	public MainViewModel Main { get; set; }

	public async Task<string> Send(string address, string amount, string[] labels)
	{
		// TODO:

		return $"Sending {amount} to {address}, {labels}...";
	}

	public async Task<string> Receive(string[] labels)
	{
		// TODO: Show receive dialog but QR cod progress view.

		var resultMessage = "";

		if (MainViewModel.Instance.CurrentWallet is { } currentWallet)
		{
			var currentWalletValue = await currentWallet.LastOrDefaultAsync();
			if (currentWalletValue is { })
			{
				// TODO:
				if (currentWalletValue.ReceiveCommand.CanExecute(null))
				{
					currentWalletValue.ReceiveCommand.Execute(null);
					resultMessage = "Generating receive address...";
				}
				else
				{
					resultMessage = "Can't generate receive address.";
				}
			}
			else
			{
				resultMessage = "Please select current wallet.";
			}
		}
		else
		{
			resultMessage = "Can't generate receive address.";
		}

		return resultMessage;
	}

	public async Task<string> Balance()
	{
		// TODO:

		var resultMessage = "";

		if (MainViewModel.Instance.CurrentWallet is { } currentWallet)
		{
			var currentWalletValue = await currentWallet.LastOrDefaultAsync();
			if (currentWalletValue is { })
			{
				var balance = currentWalletValue.Wallet.Coins.TotalAmount().ToFormattedString();

				resultMessage = $"{currentWalletValue.Wallet.WalletName} balance is {balance}";
			}
			else
			{
				resultMessage = "Please select current wallet.";
			}
		}
		else
		{
			resultMessage = "Can not provide wallet balance.";
		}

		return resultMessage;
	}
}
