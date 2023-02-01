using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using NBitcoin;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.Affiliation.Models.CoinjoinRequest;
using WalletWasabi.Affiliation.Extensions;
using System.Collections.Generic;

namespace WalletWasabi.Affiliation;

public class RoundData
{
	public RoundData(RoundParameters roundParameters)
	{
		RoundParameters = roundParameters;
		OutpointCoinPairs = new();
		OutpointAffiliationPairs = new();
		OutpointFeeExemptionPairs = new();

	}

	private RoundParameters RoundParameters { get; }

	private ConcurrentBag<Tuple<OutPoint, Coin>> OutpointCoinPairs { get; }
	private ConcurrentBag<Tuple<OutPoint, AffiliationFlag>> OutpointAffiliationPairs { get; }
	private ConcurrentBag<Tuple<OutPoint, bool>> OutpointFeeExemptionPairs { get; }

	public void AddInputCoin(Coin coin)
	{
		OutpointCoinPairs.Add(new Tuple<OutPoint, Coin>(coin.Outpoint, coin));
	}

	public void AddInputAffiliationFlag(Coin coin, AffiliationFlag affiliationFlag)
	{
		OutpointAffiliationPairs.Add(new Tuple<OutPoint, AffiliationFlag>(coin.Outpoint, affiliationFlag));
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

		foreach (OutPoint outpoint in pairsOutpoints.Except((IEnumerable<OutPoint>)outpoints))
		{
			Logging.Logger.LogInfo($"Unnecessary {name} for outpoint {Convert.ToHexString(outpoint.ToBytes())}.");
		}

		Dictionary<OutPoint, TValue> valuesByOutpoints = pairsGroupedByOutpoints.ToDictionary(g => g.Key, g => g.First().Item2);
		return valuesByOutpoints;
	}


	public FinalizedRoundData FinalizeRoundData(NBitcoin.Transaction transaction)
	{
		HashSet<OutPoint> transactionOutpoints = transaction.Inputs.Select(x => x.PrevOut).ToHashSet();
		Dictionary<OutPoint, AffiliationFlag> affiliationFlagsByOutpoints = GetDictionary(OutpointAffiliationPairs, transactionOutpoints, "affiliation flag");
		Dictionary<OutPoint, bool> feeExemptionsByOutpoints = GetDictionary(OutpointFeeExemptionPairs, transactionOutpoints, "fee exemptions");
		Dictionary<OutPoint, Coin> coinByOutpoints = GetDictionary(OutpointCoinPairs, transactionOutpoints, "coin");

		Func<Money, bool> isNoFee = amount => RoundParameters.CoordinationFeeRate.GetFee(amount) == Money.Zero;

		IEnumerable<AffiliateInput> inputs = transaction.Inputs.Select(x => new AffiliateInput(coinByOutpoints[x.PrevOut], affiliationFlagsByOutpoints.GetValueOrDefault(x.PrevOut, AffiliationFlag.Default), feeExemptionsByOutpoints.GetValueOrDefault(x.PrevOut, false) || isNoFee(coinByOutpoints[x.PrevOut].Amount)));

		return new FinalizedRoundData(inputs, transaction.Outputs, RoundParameters.Network, RoundParameters.CoordinationFeeRate, RoundParameters.AllowedInputAmounts.Min);
	}
}
