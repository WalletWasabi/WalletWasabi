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
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;

#pragma warning disable WW001 // Do not use UiContext or Navigation APIs in ViewModel Constructor
#pragma warning disable WW002 // Make ViewModel Constructor private

public partial class PayNowAssistantMessageViewModel : AssistantMessageViewModel
{
	private readonly Workflow _workflow;

	[AutoNotify] private bool _isBusy;

	public PayNowAssistantMessageViewModel(Workflow workflow, ChatMessage message) : base(message)
	{
		if (message.Data is not Invoice invoice)
		{
			throw new InvalidOperationException($"Invalid Data Type: {message.Data?.GetType().Name}");
		}

		_workflow = workflow;

		Invoice = invoice;
		Amount = invoice.Amount;
		Address = invoice.BitcoinAddress;
		IsPaid = invoice.IsPaid;
		PaymentUrl = invoice.Bip21Link;
		PayButtonText = IsPaid ? "Paid" : "Pay";
		UiMessage = message.Text;

		UiContext = UiContext.Default;
		PayNowCommand = ReactiveCommand.CreateFromTask(PayNowAsync, this.WhenAnyValue(x => x.IsPaid).Select(x => !x));
	}

	public string PayButtonText { get; set; }

	public string PaymentUrl { get; set; }

	public Invoice Invoice { get; }

	public UiContext UiContext { get; set; }

	public decimal Amount { get; }

	public string Address { get; }

	public bool IsPaid { get; }

	public ICommand PayNowCommand { get; }

	private async Task PayNowAsync()
	{
		// TODO: This is a dirty hack to obtain the current wallet. Please remove it before merging.
		var walletVm = MainViewModel.Instance.NavBar.SelectedWallet;
		if (walletVm == null)
		{
			return;
		}

		var transactionInfo = new TransactionInfo(BitcoinAddress.Create(Address, walletVm.Wallet.Network), walletVm.Wallet.AnonScoreTarget)
		{
			Amount = new Money(Amount, MoneyUnit.BTC),
			Recipient = new LabelsArray("Buy Anything Agent"),
			IsFixedAmount = true
		};

		// TODO: We don't want to have Wallet, but IWalletModel instead.
		await SendAsync(walletVm.Wallet, transactionInfo, walletVm.WalletModel);
	}

	private async Task SendAsync(Wallet wallet, TransactionInfo info, IWalletModel walletModel)
	{
		try
		{
			var transaction = await Task.Run(() => TransactionHelpers.BuildTransactionForSIB(wallet, info));
			var transactionAuthorizationInfo = new TransactionAuthorizationInfo(transaction);
			var authResult = await AuthorizeAsync(wallet, walletModel, transactionAuthorizationInfo);
			if (authResult)
			{
				IsBusy = true;

				await Services.TransactionBroadcaster.SendTransactionAsync(transaction.Transaction);
				wallet.UpdateUsedHdPubKeysLabels(transaction.HdPubKeysWithNewLabels);

				var updatedMessage = Message with { Data = Invoice with { IsPaid = true } };
				var updatedConversation = _workflow.Conversation.ReplaceMessage(Message, updatedMessage);
				_workflow.Conversation = updatedConversation;
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await UiContext.Default.Navigate().CompactDialogScreen.ShowErrorAsync("Transaction", ex.ToUserFriendlyString(), "Wasabi was unable to send your transaction.");
		}
		finally
		{
			IsBusy = false;
		}
	}

	private async Task<bool> AuthorizeAsync(Wallet wallet, IWalletModel walletModel, TransactionAuthorizationInfo transactionAuthorizationInfo)
	{
		if (!wallet.KeyManager.IsHardwareWallet &&
			string.IsNullOrEmpty(wallet.Kitchen.SaltSoup())) // Do not show authentication dialog when password is empty
		{
			return true;
		}

		var authDialog = AuthorizationHelpers.GetAuthorizationDialog(walletModel, transactionAuthorizationInfo);
		var authDialogResult = await UiContext.Default.Navigate().NavigateDialogAsync(authDialog, authDialog.DefaultTarget, NavigationMode.Clear);

		return authDialogResult.Result;
	}
}
