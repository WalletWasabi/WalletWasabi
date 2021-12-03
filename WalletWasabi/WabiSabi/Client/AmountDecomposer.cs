using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.Decomposition;

namespace WalletWasabi.WabiSabi.Client
{
	public class AmountDecomposer
	{
		public AmountDecomposer(IEnumerable<SmartCoin> possibleInputCoins, RoundState roundState, int maxAvailableVsize)
		{
			RoundState = roundState;

			// TODO limit to round imposed value
			var maxEffectiveValue = possibleInputCoins.Sum(x => x.EffectiveValue(RoundState.FeeRate));

			var allowedStandardValues = StandardDenomination.Values.Where(x => roundState.CoinjoinState.Parameters.AllowedOutputAmounts.Contains(x));

			// TODO allow more than 5 outputs after compact representation, because until then the memory consumption is unreasonable
			var maxOutputs = Math.Min(maxAvailableVsize / Constants.P2WPKHOutputSizeInBytes, 5);

			Logger.LogDebug($"Computing possible decompositions up to {maxOutputs} outputs ({maxAvailableVsize} vbytes available) for {maxEffectiveValue}.");

			PossibleDecompositions = new PossibleDecompositions(
				allowedStandardValues,
				maxEffectiveValue, // TODO needs to be higher to evaluate other users' inputs
				Money.Zero,
				maxOutputs);

			Logger.LogDebug($"At {roundState.FeeRate}, best decomposition is {string.Join(' ', PossibleDecompositions.GetByTotalValue(feeRate: roundState.FeeRate).First().Outputs)}.");
		}

		public RoundState RoundState { get; }

		private PossibleDecompositions PossibleDecompositions { get; }

		public IEnumerable<Money> Decompose(IEnumerable<Coin> myInputCoins, IEnumerable<Coin> allInputCoins, int availableVsize)
		{
			var maximumEffectiveCost = myInputCoins.Select(x => x.EffectiveValue(RoundState.FeeRate)).Sum();
			var minimumEffectiveCost = Money.Min(maximumEffectiveCost.Percentage(99.99m), maximumEffectiveCost - 5000L);

			Logger.LogDebug($"Choosing decomposition between {minimumEffectiveCost} and {maximumEffectiveCost} with {availableVsize} vbytes max at feerate {RoundState.FeeRate}.");

			var chosen = PossibleDecompositions.GetByTotalValue(
				maxDecompositions: 100,
				maximumEffectiveCost: maximumEffectiveCost,
				minimumEffectiveCost: minimumEffectiveCost,
				feeRate: RoundState.FeeRate,
				maxOutputs: availableVsize / 31)
				.RandomElement();

			if (chosen is null)
			{
				throw new InvalidOperationException("No decompositions were possible.");
			}

			Logger.LogDebug($"Decomposing as {string.Join(' ', chosen.Outputs)} = {chosen.TotalValue} ({maximumEffectiveCost - chosen.TotalValue} cost).");
			return chosen.Outputs;
		}
	}
}
