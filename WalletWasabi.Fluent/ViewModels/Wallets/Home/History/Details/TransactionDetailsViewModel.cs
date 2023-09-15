using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

[NavigationMetaData(Title = "Transaction Details")]
public partial class TransactionDetailsViewModel : RoutableViewModel
{
	private readonly WalletViewModel _walletVm;

	[AutoNotify] private bool _isConfirmed;
	[AutoNotify] private int _confirmations;
	[AutoNotify] private int _blockHeight;
	[AutoNotify] private string? _dateString;
	[AutoNotify] private Money? _amount;
	[AutoNotify] private LabelsArray? _labels;
	[AutoNotify] private string? _transactionId;
	[AutoNotify] private string? _blockHash;
	[AutoNotify] private string? _amountText = "";
	[AutoNotify] private TimeSpan? _confirmationTime;
	[AutoNotify] private bool _isConfirmationTimeVisible;
	[AutoNotify] private bool _isLabelsVisible;

	private TransactionDetailsViewModel(SmartTransaction transaction, Money amount, WalletViewModel walletVm)
	{
		_walletVm = walletVm;

		NextCommand = ReactiveCommand.Create(OnNext);

		Fee = transaction.GetFee();
		DestinationAddresses = transaction.GetDestinationAddresses(walletVm.Wallet.Network, out _, out _).ToList();

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		if (amount < Money.Zero)
		{
			Amount = -amount - (Fee ?? Money.Zero);
			AmountText = "Outgoing";
			IsFeeVisible = Fee != null;
		}
		else
		{
			Amount = amount;
			AmountText = "Incoming";
		}

		UpdateValues(transaction);
	}

	public ICollection<BitcoinAddress> DestinationAddresses { get; }

	public bool IsFeeVisible { get; private set; }

	public Money? Fee { get; }

	private void UpdateValues(SmartTransaction transaction)
	{
		DateString = transaction.FirstSeen.ToLocalTime().ToUserFacingString();
		TransactionId = transaction.GetHash().ToString();
		Labels = transaction.Labels;
		BlockHeight = transaction.Height.Type == HeightType.Chain ? transaction.Height.Value : 0;
		Confirmations = transaction.GetConfirmations((int)Services.BitcoinStore.SmartHeaderChain.ServerTipHeight);

		TransactionFeeHelper.TryEstimateConfirmationTime(_walletVm.Wallet, transaction, out var estimate);
		ConfirmationTime = estimate;

		IsConfirmed = Confirmations > 0;

		BlockHash = transaction.BlockHash?.ToString();

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

		_walletVm.UiTriggers.TransactionsUpdateTrigger
			.DoAsync(async _ => await UpdateCurrentTransactionAsync())
			.Subscribe()
			.DisposeWith(disposables);
	}

	private async Task UpdateCurrentTransactionAsync()
	{
		await Task.Run(() =>
		{
			if (_walletVm.Wallet.BitcoinStore.TransactionStore.TryGetTransaction(uint256.Parse(TransactionId), out var currentTransaction))
			{
				UpdateValues(currentTransaction);
			}
		});
	}
}
