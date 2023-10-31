using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Models;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

[NavigationMetaData(Title = "Transaction Details")]
public partial class TransactionDetailsViewModel : RoutableViewModel
{
	private readonly IWalletModel _wallet;

	[AutoNotify] private bool _isConfirmed;
	[AutoNotify] private string? _amountText = "";
	[AutoNotify] private string? _blockHash;
	[AutoNotify] private int _blockHeight;
	[AutoNotify] private int _confirmations;
	[AutoNotify] private TimeSpan? _confirmationTime;
	[AutoNotify] private string? _dateString;
	[AutoNotify] private bool _isConfirmationTimeVisible;
	[AutoNotify] private bool _isLabelsVisible;
	[AutoNotify] private LabelsArray? _labels;
	[AutoNotify] private Amount? _amount;

	public TransactionDetailsViewModel(UiContext uiContext, IWalletModel wallet, TransactionSummary transactionSummary)
	{
		UiContext = uiContext;
		_wallet = wallet;

		NextCommand = ReactiveCommand.Create(OnNext);
		Fee = uiContext.AmountProvider.Create(transactionSummary.GetFee());
		if (Fee is null || !Fee.HasBalance)
		{
			Fee = uiContext.AmountProvider.Create(wallet.TransactionFeeProvider.GetFee(transactionSummary.GetHash()));
		}

		IsFeeVisible = Fee != null && Fee.HasBalance;

		TransactionId = transactionSummary.GetHash();
		DestinationAddresses = transactionSummary.Transaction.GetDestinationAddresses(wallet.Network).ToArray();

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		UpdateValues(transactionSummary);
	}

	public uint256 TransactionId { get; }

	public Amount? Fee { get; }

	public ICollection<BitcoinAddress> DestinationAddresses { get; }

	public bool IsFeeVisible { get; }

	private void UpdateValues(TransactionSummary transactionSummary)
	{
		DateString = transactionSummary.FirstSeen.ToLocalTime().ToUserFacingString();
		Labels = transactionSummary.Labels;
		BlockHeight = transactionSummary.Height.Type == HeightType.Chain ? transactionSummary.Height.Value : 0;
		Confirmations = transactionSummary.GetConfirmations();

		Network network = Services.WalletManager.Network;
		int vSize = transactionSummary.Transaction.Transaction.GetVirtualSize();
		TransactionFeeHelper.TryEstimateConfirmationTime(Services.HostedServices.Get<HybridFeeProvider>(), network, Fee!.Btc, vSize, out var estimate);

		var confirmationTime = estimate;
		if (confirmationTime is { })
		{
			ConfirmationTime = confirmationTime;
		}

		IsConfirmed = Confirmations > 0;

		if (transactionSummary.Amount < Money.Zero)
		{
			Amount = UiContext.AmountProvider.Create(-transactionSummary.Amount - (transactionSummary.GetFee() ?? Money.Zero));
			AmountText = "Amount sent";
		}
		else
		{
			Amount = UiContext.AmountProvider.Create(transactionSummary.Amount);
			AmountText = "Amount received";
		}

		BlockHash = transactionSummary.BlockHash?.ToString();

		IsConfirmationTimeVisible = ConfirmationTime.HasValue && ConfirmationTime != TimeSpan.Zero;
		IsLabelsVisible = Labels.HasValue && Labels.Value.Any();
	}

	private void OnNext()
	{
		Navigate().Clear();
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		_wallet.Transactions.TransactionProcessed
							.Do(_ => UpdateCurrentTransaction())
							.Subscribe()
							.DisposeWith(disposables);
	}

	private void UpdateCurrentTransaction()
	{
		if (_wallet.Transactions.TryGetById(TransactionId, false, out var transaction))
		{
			UpdateValues(transaction.TransactionSummary);
		}
	}
}
