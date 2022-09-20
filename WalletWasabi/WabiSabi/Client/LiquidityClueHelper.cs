using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis;
using WalletWasabi.Extensions;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client;

public class LiquidityClueHelper
{
	private static Money? LiquidityClue { get; set; } = null;
	private static object LiquidityClueLock { get; } = new object();

	public static void InitLiquidityClue(IEnumerable<Money> foreignOutputsValues)
	{
		var newLiquidityClue = LiquidityClueHelper.TryCalculateLiquidityClue(foreignOutputsValues);

		lock (LiquidityClueLock)
		{
			if (LiquidityClue is null)
			{
				LiquidityClue = newLiquidityClue;
			}
		}
	}

	public static void InitLiquidityClue(Transaction lastCoinjoin, IEnumerable<TxOut> walletTxOuts)
	{
		InitLiquidityClue(GetForeignOutputsValues(lastCoinjoin, walletTxOuts));
	}

	public async Task InitLiquidityClue(IWallet wallet)
	{
		var lastCoinjoin = (await wallet.GetTransactionsAsync().ConfigureAwait(false)).OrderByBlockchain().LastOrDefault(x => x.IsOwnCoinjoin());

		if (lastCoinjoin is not null)
		{
			InitLiquidityClue(lastCoinjoin.Transaction, lastCoinjoin.WalletOutputs.Select(x => x.TxOut));
		}
	}

	public static Money GetLiquidityClue(Money maxSuggestedAmount)
	{
		Money liquidityClue = maxSuggestedAmount;
		lock (LiquidityClueLock)
		{
			if (LiquidityClue is not null)
			{
				liquidityClue = Math.Min(LiquidityClue, liquidityClue);
			}
		}
		return liquidityClue;
	}

	public static Money GetLiquidityClue(RoundParameters roundParameters)
	{
		return GetLiquidityClue(roundParameters.MaxSuggestedAmount);
	}

	public void UpdateLiquidityClue(Money maxSuggestedAmount, IEnumerable<Money> foreignOutputsValues)
	{
		Money? liquidityClue = TryCalculateLiquidityClue(foreignOutputsValues);

		// Dismiss pleb round.
		// If it's close to the max suggested amount then we shouldn't set it as the round is likely a pleb round.
		if (liquidityClue is not null && (maxSuggestedAmount / 2) > liquidityClue)
		{
			lock (LiquidityClueLock)
			{
				LiquidityClue = liquidityClue;
			}
		}
	}

	public void UpdateLiquidityClue(RoundParameters roundParameters, Transaction unsignedCoinJoin, IEnumerable<TxOut> walletTxOuts)
	{
		UpdateLiquidityClue(roundParameters.MaxSuggestedAmount, GetForeignOutputsValues(unsignedCoinJoin, walletTxOuts));
	}

	private static IEnumerable<Money> GetForeignOutputsValues(Transaction transaction, IEnumerable<TxOut> walletTxOuts)
	{
		return transaction.Outputs
			.Where(x => !walletTxOuts?.Any(y => y.ScriptPubKey == x.ScriptPubKey && y.Value == x.Value) is true) // We only care about outputs those aren't ours.
			.Select(x => x.Value);
	}

	private static Money? TryCalculateLiquidityClue(IEnumerable<Money> foreignOutputsValues)
	{
		var denoms = foreignOutputsValues
				.Where(x => BlockchainAnalyzer.StdDenoms.Contains(x.Satoshi)) // We only care about denom outputs as those can be considered reasonably mixed.
				.OrderByDescending(x => x)
				.Distinct()
				.ToArray();
		var topDenoms = denoms.Take((int)Math.Ceiling(denoms.Length * 10 / 100d)); // Take top 10% of denominations.
		if (topDenoms.Any())
		{
			return Money.Coins(topDenoms.Average(x => x.ToDecimal(MoneyUnit.BTC)));
		}
		else
		{
			return null;
		}
	}
}
