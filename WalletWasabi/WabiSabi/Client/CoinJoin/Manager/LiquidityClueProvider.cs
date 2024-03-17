using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client;

public class LiquidityClueProvider
{
	public LiquidityClueProvider()
	{
		LiquidityClue = null;
		LiquidityClueLock = new object();
	}

	private Money? LiquidityClue { get; set; }
	private object LiquidityClueLock { get; }

	public void InitLiquidityClue(IEnumerable<Money> foreignOutputsValues)
	{
		lock (LiquidityClueLock)
		{
			if (TryCalculateLiquidityClue(foreignOutputsValues, out var newLiquidityClue))
			{
				LiquidityClue = newLiquidityClue;
			}
		}
	}

	public void InitLiquidityClue(Transaction lastCoinJoin, IEnumerable<TxOut> walletTxOuts) =>
		InitLiquidityClue(GetForeignOutputsValues(lastCoinJoin, walletTxOuts));

	public async Task InitLiquidityClueAsync(IWallet wallet)
	{
		var transactions = await wallet.GetTransactionsAsync().ConfigureAwait(false);
		if (transactions.LastOrDefault(x => x.IsOwnCoinjoin()) is { } lastCoinJoin)
		{
			InitLiquidityClue(lastCoinJoin.Transaction, lastCoinJoin.WalletOutputs.Select(x => x.TxOut));
		}
	}

	public Money GetLiquidityClue(Money maxSuggestedAmount)
	{
		lock (LiquidityClueLock)
		{
			return Math.Min(
				LiquidityClue ?? Constants.MaximumNumberOfBitcoinsMoney,
				maxSuggestedAmount);
		}
	}

	private void UpdateLiquidityClue(Money maxSuggestedAmount, IEnumerable<Money> foreignOutputsValues)
	{
		// Dismiss pleb round.
		// If it's close to the max suggested amount then we shouldn't set it as the round is likely a pleb round.
		lock (LiquidityClueLock)
		{
			if (TryCalculateLiquidityClue(foreignOutputsValues, out var liquidityClue) && (maxSuggestedAmount / 2) > liquidityClue)
			{
				LiquidityClue = liquidityClue;
			}
		}
	}

	public void UpdateLiquidityClue(Money maxSuggestedAmount, Transaction unsignedCoinJoin, IEnumerable<TxOut> walletTxOuts) =>
		UpdateLiquidityClue(maxSuggestedAmount, GetForeignOutputsValues(unsignedCoinJoin, walletTxOuts));

	public IEnumerable<Money> GetForeignOutputsValues(Transaction transaction, IEnumerable<TxOut> walletTxOuts) =>
		 transaction.Outputs
			.Except(walletTxOuts, TxOutEqualityComparer.Default)
			.Select(x => x.Value);

	private static bool TryCalculateLiquidityClue(IEnumerable<Money> foreignOutputsValues, out Money? value)
	{
		var denoms = foreignOutputsValues
			.Where(x => BlockchainAnalyzer.StdDenoms.Contains(x.Satoshi)) // We only care about denom outputs as those can be considered reasonably mixed.
			.Distinct()
			.ToList();

		var take = (int)Math.Ceiling(denoms.Count * 0.1); // Take top 10% of denominations.
		var topDenoms = denoms
			.OrderByDescending(x => x)
			.Take(take);

		value = Money.Satoshis((ulong)topDenoms.DefaultIfEmpty(Money.Zero).Average(x => x.Satoshi));
		return value > Money.Zero;
	}
}
