using NBitcoin;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.Affiliation;

public class RoundData
{
	public RoundData(RoundParameters roundParameters)
	{
		RoundParameters = roundParameters;
	}

	private RoundParameters RoundParameters { get; }

	private ConcurrentBag<Tuple<OutPoint, Coin>> OutpointCoinPairs { get; } = new();
	private ConcurrentBag<Tuple<OutPoint, string>> OutpointAffiliationPairs { get; } = new();
	private ConcurrentBag<Tuple<OutPoint, bool>> OutpointFeeExemptionPairs { get; } = new();

	public void AddInputCoin(Coin coin)
	{
		OutpointCoinPairs.Add(new Tuple<OutPoint, Coin>(coin.Outpoint, coin));
	}

	public void AddInputAffiliationFlag(Coin coin, string affiliationFlag)
	{
		OutpointAffiliationPairs.Add(new Tuple<OutPoint, string>(coin.Outpoint, affiliationFlag));
	}

	public void AddInputFeeExemption(Coin coin, bool isCoordinationFeeExempted)
	{
		OutpointFeeExemptionPairs.Add(new Tuple<OutPoint, bool>(coin.Outpoint, isCoordinationFeeExempted));
	}

	private Dictionary<OutPoint, TValue> GetDictionary<TValue>(IEnumerable<Tuple<OutPoint, TValue>> pairs, IEnumerable<OutPoint> outpoints, string name)
	{
		IEnumerable<IGrouping<OutPoint, Tuple<OutPoint, TValue>>> pairsGroupedByOutpoints = pairs.GroupBy(x => x.Item1);

		foreach (IGrouping<OutPoint, Tuple<OutPoint, TValue>> pairGroup in pairsGroupedByOutpoints.Where(g => g.Count() > 1))
		{
			Logging.Logger.LogWarning($"Duplicate {name} for outpoint {Convert.ToHexString(pairGroup.Key.ToBytes())}.");
		}

		HashSet<OutPoint> pairsOutpoints = pairsGroupedByOutpoints.Select(g => g.Key).ToHashSet();

		foreach (OutPoint outpoint in outpoints.Except(pairsOutpoints))
		{
			Logging.Logger.LogWarning($"Missing {name} for outpoint {Convert.ToHexString(outpoint.ToBytes())}.");
		}

		foreach (OutPoint outpoint in pairsOutpoints.Except(outpoints))
		{
			Logging.Logger.LogInfo($"Unnecessary {name} for outpoint {Convert.ToHexString(outpoint.ToBytes())}.");
		}

		Dictionary<OutPoint, TValue> valuesByOutpoints = pairsGroupedByOutpoints.ToDictionary(g => g.Key, g => g.First().Item2);
		return valuesByOutpoints;
	}

	public FinalizedRoundData FinalizeRoundData(Transaction transaction)
	{
		HashSet<OutPoint> transactionOutpoints = transaction.Inputs.Select(x => x.PrevOut).ToHashSet();
		Dictionary<OutPoint, string> affiliationFlagsByOutpoints = GetDictionary(OutpointAffiliationPairs, transactionOutpoints, "affiliation flag");
		Dictionary<OutPoint, bool> feeExemptionsByOutpoints = GetDictionary(OutpointFeeExemptionPairs, transactionOutpoints, "fee exemptions");
		Dictionary<OutPoint, Coin> coinByOutpoints = GetDictionary(OutpointCoinPairs, transactionOutpoints, "coin");

		Func<Money, bool> isNoFee = amount => RoundParameters.CoordinationFeeRate.GetFee(amount) == Money.Zero;

		IEnumerable<AffiliateInput> inputs = transaction.Inputs
			.Select(x => coinByOutpoints[x.PrevOut])
			.Select(x => new AffiliateInput(x.Outpoint, x.ScriptPubKey, affiliationFlagsByOutpoints.GetValueOrDefault(x.Outpoint, AffiliationFlagConstants.Default), feeExemptionsByOutpoints.GetValueOrDefault(x.Outpoint, false) || isNoFee(x.Amount)));

		return new FinalizedRoundData(inputs, transaction.Outputs, RoundParameters.Network, RoundParameters.CoordinationFeeRate, RoundParameters.AllowedInputAmounts.Min);
	}
}
