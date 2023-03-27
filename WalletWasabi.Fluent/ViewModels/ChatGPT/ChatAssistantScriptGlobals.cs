using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.ChatGPT.Messages.Actions;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.ChatGPT;

public class ChatAssistantScriptGlobals
{
	public ChatAssistantViewModel Chat { get; set; }

	public MainViewModel Main { get; set; }

	public async Task<string> Send(string address, decimal amountBtc, string[] labels)
	{
		var resultMessage = "";

		if (MainViewModel.Instance.MainScreen.CurrentPage is WalletViewModel currentWallet)
		{
			Chat.Messages.Add(new SendActionMessageViewModel()
			{
				Message = $"Sending {amountBtc} to {address}, {new SmartLabel(labels)}..."
			});

			Chat.IsBusy = true;

			var IsFixedAmount = false;
			var IsPayJoin = false;

			// TODO:
			var amount = new Money(amountBtc, MoneyUnit.BTC);
			var transactionInfo = new TransactionInfo(BitcoinAddress.Create(address, currentWallet.Wallet.Network), currentWallet.Wallet.AnonScoreTarget)
			{
				Amount = amount,
				Recipient = new SmartLabel(labels),
				// TODO: PayJoinClient = GetPayjoinClient(PayJoinEndPoint),
				PayJoinClient = null,
				IsFixedAmount = IsFixedAmount,
				SubtractFee = amount == currentWallet.Wallet.Coins.TotalAmount() && !(IsFixedAmount || IsPayJoin)
			};

			var transaction = await Task.Run(() => TransactionHelpers.BuildTransaction(currentWallet.Wallet, transactionInfo));
			var transactionAuthorizationInfo = new TransactionAuthorizationInfo(transaction);
			// TODO:
			//var authResult = await AuthorizeAsync(transactionAuthorizationInfo);
			//if (authResult)
			{
				try
				{
					var finalTransaction =
						await GetFinalTransactionAsync(transactionAuthorizationInfo.Transaction, transactionInfo);
					await SendTransactionAsync(finalTransaction);
					// _cancellationTokenSource?.Cancel();
					//Navigate().To(new SendSuccessViewModel(currentWallet.Wallet, finalTransaction));
					Chat.Messages.Add(new SendActionMessageViewModel()
					{
						Message = $"Sent {amountBtc} to {address}, {labels}."
					});
				}
				catch (Exception ex)
				{
					Logger.LogError(ex);
					// TODO:
					//await ShowErrorAsync("Transaction", ex.ToUserFriendlyString(),
					//	"Wasabi was unable to send your transaction.");
				}
			}

			async Task<SmartTransaction> GetFinalTransactionAsync(SmartTransaction transaction,
				TransactionInfo transactionInfo)
			{
				if (transactionInfo.PayJoinClient is { })
				{
					try
					{
						var payJoinTransaction = await Task.Run(() =>
							TransactionHelpers.BuildTransaction(currentWallet.Wallet, transactionInfo, isPayJoin: true));
						return payJoinTransaction.Transaction;
					}
					catch (Exception ex)
					{
						Logger.LogError(ex);
					}
				}

				return transaction;
			}

			async Task SendTransactionAsync(SmartTransaction transaction)
			{
				await Services.TransactionBroadcaster.SendTransactionAsync(transaction);
			}


			Chat.IsBusy = false;
		}
		else
		{
			resultMessage = "Can't generate receive address, please login into wallet.";
		}

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
