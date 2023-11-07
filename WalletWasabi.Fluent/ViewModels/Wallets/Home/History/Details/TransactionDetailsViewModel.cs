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
	[AutoNotify] private Amount? _fee;
	[AutoNotify] private bool _isFeeVisible;

	public TransactionDetailsViewModel(UiContext uiContext, IWalletModel wallet, TransactionSummary transactionSummary)
	{
		UiContext = uiContext;
		_wallet = wallet;

		NextCommand = ReactiveCommand.Create(OnNext);

		TransactionId = transactionSummary.GetHash();
		DestinationAddresses = transactionSummary.Transaction.GetDestinationAddresses(wallet.Network).ToArray();

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		UpdateValues(transactionSummary);
	}

	public uint256 TransactionId { get; }

	public ICollection<BitcoinAddress> DestinationAddresses { get; }

	private void UpdateValues(TransactionSummary transactionSummary)
	{
		DateString = transactionSummary.FirstSeen.ToLocalTime().ToUserFacingString();
		Labels = transactionSummary.Labels;
		BlockHeight = transactionSummary.Height.Type == HeightType.Chain ? transactionSummary.Height.Value : 0;
		Confirmations = transactionSummary.GetConfirmations();

		Fee = UiContext.AmountProvider.Create(transactionSummary.GetFee());
		IsFeeVisible = Fee != null && Fee.HasBalance;

		transactionSummary.TryGetConfirmationTime(out var estimate);
		if (estimate is { })
		{
			ConfirmationTime = estimate;
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

		_wallet.Transactions.RequestedFeeArrived
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
