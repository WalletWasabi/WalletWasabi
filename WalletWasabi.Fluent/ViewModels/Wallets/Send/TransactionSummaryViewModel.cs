using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.CoinControl;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class TransactionSummaryViewModel : ViewModelBase
{
	private readonly Wallet _wallet;
	private BuildTransactionResult? _transaction;
	[AutoNotify] private string _amountText = "";
	[AutoNotify] private bool _transactionHasChange;
	[AutoNotify] private bool _transactionHasPockets;
	[AutoNotify] private string _confirmationTimeText = "";
	[AutoNotify] private string _feeText = "";
	[AutoNotify] private bool _maxPrivacy;
	[AutoNotify] private bool _isCustomFeeUsed;
	[AutoNotify] private bool _isOtherPocketSelectionPossible;
	[AutoNotify] private SmartLabel _labels = SmartLabel.Empty;
	[AutoNotify] private SmartLabel _recipient = SmartLabel.Empty;
	[AutoNotify] private bool _isCoinControlVisible;

	public TransactionSummaryViewModel(TransactionPreviewViewModel parent, WalletViewModel walletViewModel, Wallet wallet, TransactionInfo info, bool isPreview = false)
	{
		Parent = parent;
		_wallet = wallet;
		IsPreview = isPreview;

		this.WhenAnyValue(x => x.TransactionHasChange, x => x.TransactionHasPockets)
			.Subscribe(_ => MaxPrivacy = !TransactionHasPockets && !TransactionHasChange);

		AddressText = info.Destination.ToString();
		PayJoinUrl = info.PayJoinClient?.PaymentUrl.AbsoluteUri;
		IsPayJoin = PayJoinUrl is not null;

		var selectCoinsInteraction = SelectCoinsInteraction(walletViewModel);

		SelectCoinsCommand = ReactiveCommand.CreateFromObservable(() => selectCoinsInteraction.Handle(info));
		SelectCoinsCommand.Do(
			list =>
			{
				info.Coins = list;

				// UpdateTransactionSummary here, but how??
			}).Subscribe();
	}

	private static Interaction<TransactionInfo, List<SmartCoin>> SelectCoinsInteraction(WalletViewModel walletViewModel)
	{
		var selectCoinsInteraction = new Interaction<TransactionInfo, List<SmartCoin>>();
		selectCoinsInteraction.RegisterHandler(
			async context =>
			{
				var navigateDialog = await MainViewModel.Instance.DialogScreen.NavigateDialogAsync(new SelectCoinsDialogViewModel(walletViewModel));

				if (navigateDialog.Kind == DialogResultKind.Normal)
				{
					context.SetOutput(navigateDialog.Result!.ToList());
				}

				context.SetOutput(new List<SmartCoin>());
			});
		return selectCoinsInteraction;
	}

	public ReactiveCommand<Unit, List<SmartCoin>> SelectCoinsCommand { get; }

	public TransactionPreviewViewModel Parent { get; }

	public bool IsPreview { get; }

	public string AddressText { get; }

	public string? PayJoinUrl { get; }

	public bool IsPayJoin { get; }

	public void UpdateTransaction(BuildTransactionResult transactionResult, TransactionInfo info)
	{
		_transaction = transactionResult;

		ConfirmationTimeText = $"Approximately {TextHelpers.TimeSpanToFriendlyString(info.ConfirmationTimeSpan)} ";

		var destinationAmount = _transaction.CalculateDestinationAmount();
		AmountText = $"{destinationAmount.ToFormattedString()} BTC";

		var fee = _transaction.Fee;
		FeeText = fee.ToFeeDisplayUnitString();

		var exchangeRate = _wallet.Synchronizer.UsdExchangeRate;
		if (exchangeRate != 0)
		{
			var fiatAmountText = destinationAmount.BtcToUsd(exchangeRate).ToUsdAproxBetweenParens();
			AmountText += $" {fiatAmountText}";

			var fiatFeeText = fee.BtcToUsd(exchangeRate).ToUsdAproxBetweenParens();
			FeeText += $" {fiatFeeText}";
		}

		TransactionHasChange =
			_transaction.InnerWalletOutputs.Any(x => x.ScriptPubKey != info.Destination.ScriptPubKey);

		Labels = new SmartLabel(transactionResult.SpentCoins.SelectMany(x => x.GetLabels(info.PrivateCoinThreshold)).Except(info.Recipient.Labels));
		TransactionHasPockets = Labels.Any();

		Recipient = info.Recipient;

		IsCustomFeeUsed = info.IsCustomFeeUsed;
		IsOtherPocketSelectionPossible = info.IsOtherPocketSelectionPossible;
	}
}
