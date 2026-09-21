using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

public class TransactionTreeBuilder
{
	private readonly Wallet _wallet;
	private readonly IServices _services;

	public TransactionTreeBuilder(Wallet wallet, IServices services)
	{
		_wallet = wallet;
		_services = services;
	}

	public async Task<IEnumerable<TransactionModel>> BuildAsync(List<TransactionSummary> summaries, CancellationToken cancellationToken)
	{
		var coinjoins = new List<CoinJoinTransactionModel>();

		var result = new List<TransactionModel>();

		for (var i = 0; i < summaries.Count; i++)
		{
			var item = summaries[i];

			if (!item.IsOwnCoinjoin())
			{
				result.Add(await CreateRegularAsync(i, item, cancellationToken));
			}

			if (item.IsOwnCoinjoin())
			{
				coinjoins.Add(await CreateCoinjoinTransactionAsync(i, item, cancellationToken));
			}

			if (coinjoins.Count > 0 &&
				((i + 1 < summaries.Count && !summaries[i + 1].IsOwnCoinjoin()) || // The next item is not CJ so add the group.
				 i == summaries.Count - 1)) // There is no following item in the list so add the group.
			{
				if (coinjoins.Count == 1)
				{
					result.Add(coinjoins[0]);
				}
				else
				{
					result.Add(CreateCoinjoinGroup(coinjoins));
				}

				coinjoins = new List<CoinJoinTransactionModel>();
			}
		}

		// This second iteration is necessary to transform the flat list of speed-ups into actual groups.
		// Here are the steps:
		// 1. Identify which transactions are CPFP (parents) and their children.
		// 2. Create a speed-up group with parent and children.
		// 3. Remove the previously added items from the history (they should no longer be there, but in the group).
		// 4. Add the group.
		foreach (var summary in summaries)
		{
			if (summary.Transaction.IsCPFPd)
			{
				// Group creation.
				var childrenTxs = summary.Transaction.ChildrenPayForThisTx;

				if (!TryFindHistoryItem(summary.GetHash(), result, out var parent))
				{
					continue; // If the parent transaction is not found, continue with the next summary.
				}

				var groupItems = new List<TransactionModel> { parent };
				foreach (var childTx in childrenTxs)
				{
					if (TryFindHistoryItem(childTx.GetHash(), result, out var child))
					{
						groupItems.Add(child);
					}
				}

				// If there is only one item in the group, it's not a group.
				// This can happen, for example, when CPFP occurs between user-owned wallets.
				if (groupItems.Count <= 1)
				{
					continue;
				}

				var speedUpGroup = CreateSpeedUpGroup(summary, parent, groupItems);
				result.Add(speedUpGroup);

				// Remove the items.
				result.RemoveMany(groupItems);
			}
		}

		return result;
	}

	private bool TryFindHistoryItem(uint256 txid, IEnumerable<TransactionModel> history, [NotNullWhen(true)] out TransactionModel? found)
	{
		found = history.SingleOrDefault(x => x.Id == txid);
		return found is not null;
	}

	private async Task<RegularTransactionModel> CreateRegularAsync(int index, TransactionSummary transactionSummary, CancellationToken cancellationToken)
	{
		var itemType = GetItemType(transactionSummary);
		var date = transactionSummary.FirstSeen.ToLocalTime();
		var serverHeight = _services.GetServerTipHeight();
		var confirmations = transactionSummary.GetConfirmations(serverHeight);
		var status = GetItemStatus(transactionSummary, serverHeight);
		var haveFeeEstimations = _wallet.FeeRateEstimations is not null;

		return new RegularTransactionModel(itemType)
		{
			Id = transactionSummary.GetHash(),
			Amount = transactionSummary.Amount,
			HasBeenSpedUp = transactionSummary.IsSpeedup,
			OrderIndex = index,
			Labels = transactionSummary.Labels,
			Date = date,
			DateString = date.ToUserFacingFriendlyString(),
			DateToolTipString = date.ToUserFacingString(),
			CanCancelTransaction = transactionSummary.Transaction.IsCancellable(_wallet.KeyManager) && haveFeeEstimations,
			CanSpeedUpTransaction = transactionSummary.Transaction.IsSpeedupable(_wallet.KeyManager) && haveFeeEstimations,
			Status = status,
			Confirmations = confirmations,
			BlockHeight = transactionSummary.Height is Height.ChainHeight(var h) ? h : 0u, // FIXME: this is wrong. Only confirmed txs have a BlockHeigh
			HexFunction = transactionSummary.Hex,
			WalletInputs = transactionSummary.WalletInputs,
			ForeignInputsFunction = transactionSummary.ForeignInputs,
			WalletOutputs = transactionSummary.WalletOutputs,
			ForeignOutputsFunction = transactionSummary.ForeignOutputs,
			Fee = transactionSummary.GetFee(),
			FeeRate = transactionSummary.FeeRate(),
			ConfirmedTooltip = await GetConfirmationToolTipAsync(status, confirmations, transactionSummary.Transaction, cancellationToken),
		};
	}

	private SpeedUpTransactionGroupModel CreateSpeedUpGroup(TransactionSummary transactionSummary, TransactionModel parent, IReadOnlyList<TransactionModel> children)
	{
		var isConfirmed = children.All(x => x.IsConfirmed);

		var result = new SpeedUpTransactionGroupModel(GetItemType(transactionSummary))
		{
			Id = transactionSummary.GetHash(),
			Amount = parent.Amount,
			OrderIndex = parent.OrderIndex,
			Date = parent.Date.ToLocalTime(),
			DateString = parent.DateString,
			DateToolTipString = parent.DateToolTipString,
			Confirmations = transactionSummary.GetConfirmations(_services.GetServerTipHeight()),
			BlockHeight = transactionSummary.Height is Height.ChainHeight(var h) ? h : 0u,
			HexFunction = transactionSummary.Hex,
			WalletInputs = transactionSummary.WalletInputs,
			ForeignInputsFunction = transactionSummary.ForeignInputs,
			WalletOutputs = transactionSummary.WalletOutputs,
			ForeignOutputsFunction = transactionSummary.ForeignOutputs,
			ConfirmedTooltip = parent.ConfirmedTooltip,
			Labels = parent.Labels,
			CanCancelTransaction = transactionSummary.Transaction.IsCancellable(_wallet.KeyManager),
			CanSpeedUpTransaction = transactionSummary.Transaction.IsSpeedupable(_wallet.KeyManager),
			Status =
				isConfirmed
				? TransactionStatus.Confirmed
				: TransactionStatus.Pending,
			HasBeenSpedUp = true,
			Children = children,
		};

		var dates = children.Select(tx => tx.Date).ToImmutableArray();
		var firstDate = dates.Min().ToLocalTime();
		var lastDate = dates.Max().ToLocalTime();
		if (firstDate.Day == lastDate.Day)
		{
			foreach (var child in children)
			{
				child.DateString = child.Date.ToLocalTime().ToOnlyTimeString();
			}
		}

		foreach (var child in children)
		{
			child.IsChild = true;
		}

		return result;
	}

	private CoinJoinTransactionGroupModel CreateCoinjoinGroup(IReadOnlyList<CoinJoinTransactionModel> coinjoins)
	{
		var first = coinjoins[0];

		var dates = coinjoins.Select(tx => tx.Date).ToImmutableArray();
		var firstDate = dates.Min().ToLocalTime();
		var lastDate = dates.Max().ToLocalTime();
		var isSameDay = firstDate.Day == lastDate.Day;

		foreach (var coinjoin in coinjoins)
		{
			coinjoin.IsChild = true;

			if (isSameDay)
			{
				coinjoin.DateString = coinjoin.Date.ToLocalTime().ToOnlyTimeString();
			}
		}

		return new CoinJoinTransactionGroupModel
		{
			Id = first.Id,
			OrderIndex = first.OrderIndex,
			Labels = first.Labels,
			Date = lastDate,
			DateString = lastDate.ToUserFacingFriendlyString(),
			DateToolTipString = isSameDay
				? $"{firstDate.ToUserFacingString(withTime: false)}"
				: $"{firstDate.ToUserFacingString(withTime: true)} - {lastDate.ToUserFacingString(withTime: true)}",
			Status = coinjoins.All(x => x.IsConfirmed)
				? TransactionStatus.Confirmed
				: TransactionStatus.Pending,
			ConfirmedTooltip = coinjoins.MinBy(x => x.Confirmations)?.ConfirmedTooltip ?? "",
			Amount = coinjoins.Sum(x => x.Amount),
			CoinjoinCosts = coinjoins.All(x => x.CoinjoinCosts is not null)
				? coinjoins.Aggregate(CoinjoinCosts.Zero, (total, coinjoin) => total + coinjoin.CoinjoinCosts!)
				: null,
			Children = coinjoins,
		};
	}

	private async Task<CoinJoinTransactionModel> CreateCoinjoinTransactionAsync(int index, TransactionSummary transactionSummary, CancellationToken cancellationToken)
	{
		var date = transactionSummary.FirstSeen.ToLocalTime();
		var serverHeight = _services.GetServerTipHeight();
		var confirmations = transactionSummary.GetConfirmations(serverHeight);
		var status = GetItemStatus(transactionSummary, serverHeight);

		return new CoinJoinTransactionModel
		{
			Id = transactionSummary.GetHash(),
			Amount = transactionSummary.Amount,
			OrderIndex = index,
			Date = date,
			DateString = date.ToUserFacingFriendlyString(),
			DateToolTipString = date.ToUserFacingString(),
			Labels = transactionSummary.Labels,
			Status = status,
			Confirmations = confirmations,
			HexFunction = transactionSummary.Hex,
			WalletInputs = transactionSummary.WalletInputs,
			ForeignInputsFunction = transactionSummary.ForeignInputs,
			WalletOutputs = transactionSummary.WalletOutputs,
			ForeignOutputsFunction = transactionSummary.ForeignOutputs,
			ConfirmedTooltip = await GetConfirmationToolTipAsync(status, confirmations, transactionSummary.Transaction, cancellationToken),
			FeeRate = transactionSummary.FeeRate(),
			CoinjoinCosts = _wallet.KeyManager.CoinjoinCosts.GetValueOrDefault(transactionSummary.GetHash())
		};
	}

	private TransactionType GetItemType(TransactionSummary transactionSummary)
	{
		var isSelfSpend = transactionSummary.Amount == -(transactionSummary.GetFee() ?? Money.Zero);
		if (!transactionSummary.IsCancellation && !transactionSummary.IsCPFP && isSelfSpend)
		{
			return TransactionType.SelfTransferTransaction;
		}

		if (!transactionSummary.IsCPFP && transactionSummary.Amount > Money.Zero)
		{
			return TransactionType.IncomingTransaction;
		}

		if (!transactionSummary.IsCPFP && !transactionSummary.IsCancellation && transactionSummary.Amount < Money.Zero)
		{
			return TransactionType.OutgoingTransaction;
		}

		if (transactionSummary.IsCancellation)
		{
			return TransactionType.Cancellation;
		}

		if (transactionSummary.IsCPFP)
		{
			return TransactionType.CPFP;
		}

		return TransactionType.Unknown;
	}

	private static TransactionStatus GetItemStatus(TransactionSummary transactionSummary, uint serverHeight)
	{
		return transactionSummary.IsConfirmed(serverHeight) ? TransactionStatus.Confirmed : TransactionStatus.Pending;
	}

	private async Task<string> GetConfirmationToolTipAsync(TransactionStatus status, uint confirmations, SmartTransaction smartTransaction, CancellationToken cancellationToken)
	{
		if (status == TransactionStatus.Confirmed)
		{
			return TextHelpers.GetConfirmationText(confirmations);
		}

		var friendlyString = await TransactionFeeHelper.EstimateConfirmationTimeAsync(_wallet.FeeRateEstimations, _wallet.Network, smartTransaction, _wallet.CpfpInfoProvider, cancellationToken) is { } estimate
			? TextHelpers.TimeSpanToFriendlyString(estimate)
			: "";

		return (status, friendlyString != "") switch
		{
			(TransactionStatus.Pending, true) => $"Pending (confirming in ≈ {friendlyString})",
			(TransactionStatus.Pending, false) => "Pending",
			_ => "Unknown"
		};
	}
}
