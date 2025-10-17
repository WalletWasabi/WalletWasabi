using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;

namespace WalletWasabi.FeeRateEstimation;

public class FeeRateEstimations : IEquatable<FeeRateEstimations>
{
	private static readonly int[] AllConfirmationTargets = Constants.ConfirmationTargets.Prepend(1).ToArray();
	public static readonly FeeRateEstimations Empty = new(new Dictionary<int, FeeRate>{ {0, FeeRate.Zero} });

	/// <summary>All allowed target confirmation ranges, i.e. 0-2, 2-3, 3-6, 6-18, ..., 432-1008.</summary>
	private static readonly IEnumerable<(int Start, int End)> TargetRanges = AllConfirmationTargets
		.Skip(1)
		.Zip(AllConfirmationTargets.Skip(1).Prepend(0), (x, y) => (Start: y, End: x));

	public FeeRateEstimations(IDictionary<int, FeeRate> estimations)
	{
		var filteredEstimations = estimations
			.Where(x => x.Key >= AllConfirmationTargets[0] && x.Key <= AllConfirmationTargets[^1])
			.OrderBy(x => x.Key)
			.Select(x => (ConfirmationTarget: x.Key, FeeRate: x.Value, Range: TargetRanges.First(y => y.Start < x.Key && x.Key <= y.End)))
			.GroupBy(x => x.Range, y => y, (x, y) => (Range: x, BestEstimation: y.Last()))
			.Select(x => (ConfirmationTarget: x.Range.End, FeeRate: FeeRate.Max(x.BestEstimation.FeeRate, Constants.MinRelayFeeRate)));

		// Make sure values are unique and in the correct order and fee rates are consistently decreasing.
		Estimations = [];
		var lastFeeRate = new FeeRate(Constants.MaximumNumberOfBitcoinsMoney);
		foreach (var estimation in filteredEstimations)
		{
			// Otherwise it's inconsistent data.
			if (lastFeeRate > estimation.FeeRate)
			{
				lastFeeRate = estimation.FeeRate;
				Estimations.TryAdd(estimation.ConfirmationTarget, estimation.FeeRate);
			}
		}
	}

	/// <summary>
	/// Gets the fee estimations: int: fee target, int: satoshi/vByte
	/// </summary>
	[JsonProperty]
	public Dictionary<int, FeeRate> Estimations { get; }

	/// <summary>
	/// Estimations where we try to fill out gaps for all valid time spans.
	/// </summary>
	public IReadOnlyList<(TimeSpan timeSpan, FeeRate feeRate)> WildEstimations
	{
		get
		{
			if (Estimations.Count == 0)
			{
				return [];
			}

			var timeSpan = TimeSpan.FromMinutes(20);
			var convertedEstimations = Estimations.Select(x => (timeSpan: TimeSpan.FromMinutes(x.Key * 10), feeRate: x.Value));

			var wildEstimations = new List<(TimeSpan timeSpan, FeeRate feeRate)>();
			var prevFeeRate = FeeRate.Zero;
			while (timeSpan <= TimeSpan.FromDays(7))
			{
				var smaller = convertedEstimations.LastOrDefault(x => x.timeSpan <= timeSpan);
				var bigger = convertedEstimations.FirstOrDefault(x => x.timeSpan >= timeSpan);
				if (smaller == default)
				{
					smaller = bigger;
				}
				if (bigger == default)
				{
					bigger = smaller;
				}

				var diffTime = bigger.timeSpan - smaller.timeSpan;
				var diffFeeRateSatPerByte = bigger.feeRate.SatoshiPerByte - smaller.feeRate.SatoshiPerByte;
				var distance = timeSpan - smaller.timeSpan;
				decimal ratio;
				if (diffTime == distance || diffTime == TimeSpan.Zero)
				{
					ratio = 1;
				}
				else
				{
					ratio = (decimal)(distance / diffTime);
				}

				var feeRateSatPerByte = smaller.feeRate.SatoshiPerByte + (ratio * diffFeeRateSatPerByte);

				FeeRate feeRate = new(feeRateSatPerByte);
				if (feeRate == prevFeeRate)
				{
					break;
				}
				prevFeeRate = feeRate;

				wildEstimations.Add((timeSpan, feeRate));

				if (timeSpan.TotalDays >= 1)
				{
					timeSpan = TimeSpan.FromDays(Math.Ceiling(timeSpan.TotalDays));
					timeSpan += TimeSpan.FromDays(1);
				}
				else if (timeSpan.TotalHours >= 1)
				{
					timeSpan = TimeSpan.FromHours(Math.Ceiling(timeSpan.TotalHours));
					timeSpan += TimeSpan.FromHours(1);
				}
				else if (timeSpan.TotalMinutes >= 1)
				{
					timeSpan = TimeSpan.FromMinutes(Math.Ceiling(timeSpan.TotalMinutes));
					timeSpan += TimeSpan.FromMinutes(10);
				}
			}

			return wildEstimations;
		}
	}

	public FeeRate GetFeeRate(int confirmationTarget)
	{
		// Where the target is still under or equal to the requested target.
		return Estimations
			.Last(x => x.Key <= confirmationTarget) // The last should be the largest confirmation target.
			.Value;
	}

	public bool TryEstimateConfirmationTime(SmartTransaction tx, [NotNullWhen(true)] out TimeSpan? confirmationTime)
	{
		confirmationTime = default;
		if (tx.Confirmed)
		{
			confirmationTime = TimeSpan.Zero;
			return true;
		}

		var unconfirmedChain = new[] { tx }.Concat(tx.ChildrenPayForThisTx).Concat(tx.ParentsThisTxPaysFor);

		// If we cannot estimate the fee rate of one of the unconfirmed transactions then we cannot estimate confirmation time.
		Money totalFee = Money.Zero;
		foreach (var currentTx in unconfirmedChain)
		{
			// We must have all the inputs and know the size of the tx to estimate the feerate.
			if (!currentTx.TryGetFee(out var fee) || currentTx.IsSegwitWithoutWitness)
			{
				return false;
			}
			else
			{
				totalFee += fee;
			}
		}

		var totalVsize = unconfirmedChain.Sum(x => x.Transaction.GetVirtualSize());

		confirmationTime = EstimateConfirmationTime(new FeeRate(totalFee, totalVsize));
		return true;
	}

	public TimeSpan EstimateConfirmationTime(FeeRate feeRate)
	{
		var wildEstimations = WildEstimations.ToArray();
		if (feeRate.SatoshiPerByte >= wildEstimations.First().feeRate.SatoshiPerByte * 1.5m)
		{
			return TimeSpan.FromMinutes(10);
		}
		else if (feeRate <= wildEstimations.Last().feeRate)
		{
			return wildEstimations.Last().timeSpan;
		}

		return WildEstimations.FirstOrDefault(x => x.feeRate <= feeRate).timeSpan;
	}

	#region Equality

	public override bool Equals(object? obj) => Equals(obj as FeeRateEstimations);

	public bool Equals(FeeRateEstimations? other) => this == other;

	public override int GetHashCode()
	{
		int hash = 13;
		foreach (KeyValuePair<int, FeeRate> est in Estimations)
		{
			hash ^= est.Key.GetHashCode() ^ est.Value.GetHashCode();
		}

		return hash;
	}

	public static bool operator ==(FeeRateEstimations? x, FeeRateEstimations? y)
	{
		if (ReferenceEquals(x, y))
		{
			return true;
		}

		if (x is null || y is null)
		{
			return false;
		}

		bool equal = false;
		if (x.Estimations.Count == y.Estimations.Count) // Require equal count.
		{
			equal = true;
			foreach (var pair in x.Estimations)
			{
				if (y.Estimations.TryGetValue(pair.Key, out FeeRate? value))
				{
					// Require value be equal.
					if (value != pair.Value)
					{
						equal = false;
						break;
					}
				}
				else
				{
					// Require key be present.
					equal = false;
					break;
				}
			}
		}

		return equal;
	}

	public static bool operator !=(FeeRateEstimations? x, FeeRateEstimations? y) => !(x == y);

	#endregion Equality
}
