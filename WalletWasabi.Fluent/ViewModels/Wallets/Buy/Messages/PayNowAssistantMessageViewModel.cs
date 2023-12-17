using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using System.Threading.Tasks;
using System.Windows.Input;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.BuyAnything;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;

#pragma warning disable WW001 // Do not use UiContext or Navigation APIs in ViewModel Constructor
#pragma warning disable WW002 // Make ViewModel Constructor private

public partial class PayNowAssistantMessageViewModel : AssistantMessageViewModel
{
	[AutoNotify] private string _payButtonText = "";
	[AutoNotify] private bool _isBusy;
	[AutoNotify] private bool _isPaid;

	public PayNowAssistantMessageViewModel(Conversation conversation, ChatMessage message) : base(message)
	{
		if (message.Data is not Invoice invoice)
		{
			throw new InvalidOperationException($"Invalid Data Type: {message.Data?.GetType().Name}");
		}

		Invoice = invoice;
		Amount = invoice.Amount;
		Address = invoice.BitcoinAddress;
		IsPaid = conversation.MetaData.PaymentConfirmed;
		UiMessage = $"To finalize your order, please pay {Amount} BTC in 30 minutes, the latest by {(DateTimeOffset.Now + TimeSpan.FromMinutes(30)).ToLocalTime():HH:mm}.";

		UiContext = UiContext.Default;
		PayNowCommand = ReactiveCommand.CreateFromTask(PayNowAsync, this.WhenAnyValue(x => x.IsPaid).Select(x => !x));

		this.WhenAnyValue(x => x.IsPaid)
			.Select(x => x ? "Paid" : "Pay Now")
			.BindTo(this, x => x.PayButtonText);
	}

	public Invoice Invoice { get; }

	public UiContext UiContext { get; set; }

	public decimal Amount { get; }

	public string Address { get; }

	public ICommand PayNowCommand { get; }

	private async Task PayNowAsync()
	{
		if (!UiServices.WalletManager.TryGetSelectedAndLoggedInWalletViewModel(out var walletVm))
		{
			return;
		}

		var transactionInfo = new TransactionInfo(BitcoinAddress.Create(Address, walletVm.Wallet.Network), walletVm.Wallet.AnonScoreTarget)
		{
			Amount = new Money(Amount, MoneyUnit.BTC),
			Recipient = new LabelsArray("Buy Anything Agent"),
			IsFixedAmount = true
		};

		await SendAsync(walletVm.Wallet, transactionInfo);
	}

	private async Task SendAsync(Wallet wallet, TransactionInfo info)
	{
		try
		{
			var transaction = await Task.Run(() => TransactionHelpers.BuildTransactionForSIB(wallet, info));
			var transactionAuthorizationInfo = new TransactionAuthorizationInfo(transaction);
			var authResult = await AuthorizeAsync(wallet, transactionAuthorizationInfo);
			if (authResult)
			{
				IsBusy = true;

				await Services.TransactionBroadcaster.SendTransactionAsync(transaction.Transaction);
				wallet.UpdateUsedHdPubKeysLabels(transaction.HdPubKeysWithNewLabels);
				IsPaid = true;
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await UiContext.Default.Navigate().DialogScreen.ShowErrorAsync("Transaction", ex.ToUserFriendlyString(), "Wasabi was unable to send your transaction.");
		}
		finally
		{
			IsBusy = false;
		}
	}

	private async Task<bool> AuthorizeAsync(Wallet wallet, TransactionAuthorizationInfo transactionAuthorizationInfo)
	{
		if (!wallet.KeyManager.IsHardwareWallet &&
			string.IsNullOrEmpty(wallet.Kitchen.SaltSoup())) // Do not show authentication dialog when password is empty
		{
			return true;
		}

		var authDialog = AuthorizationHelpers.GetAuthorizationDialog(wallet, transactionAuthorizationInfo);
		var authDialogResult = await UiContext.Default.Navigate().NavigateDialogAsync(authDialog, authDialog.DefaultTarget, NavigationMode.Clear);

		return authDialogResult.Result;
	}
}
