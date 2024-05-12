using NBitcoin;
using System.Collections.Generic;
using WabiSabi.Crypto.Randomness;

namespace WalletWasabi.Models;

public sealed class CoinjoinSkipFactors : IEquatable<CoinjoinSkipFactors>
{
	public CoinjoinSkipFactors(double daily, double weekly, double monthly)
	{
		Daily = Math.Max(0d, Math.Min(daily, 1d));
		Weekly = Math.Max(0d, Math.Min(weekly, 1d));
		Monthly = Math.Max(0d, Math.Min(monthly, 1d));
	}

	public static CoinjoinSkipFactors NoSkip => new(1, 1, 1);
	public static CoinjoinSkipFactors SpeedMaximizing => new(0.7, 0.8, 0.9);
	public static CoinjoinSkipFactors CostMinimizing => new(0.1, 0.2, 0.3);
	public static CoinjoinSkipFactors PrivacyMaximizing => new(0.5, 0.5, 0.5);

	public double Daily { get; }
	public double Weekly { get; }
	public double Monthly { get; }

	private (uint256 roundId, bool judgement) LastJudgement { get; set; } = (uint256.Zero, true);

	public static CoinjoinSkipFactors FromString(string str)
	{
		var parts = str.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		if (!double.TryParse(parts[0], out var daily))
		{
			daily = 1d;
		}

		if (!double.TryParse(parts[1], out var weekly))
		{
			weekly = 1d;
		}

		if (!double.TryParse(parts[2], out var monthly))
		{
			monthly = 1d;
		}

		var factors = new CoinjoinSkipFactors(daily, weekly, monthly);
		return factors;
	}

	public bool ShouldSkipRoundRandomly(WasabiRandom random, FeeRate roundFeeRate, IDictionary<TimeSpan, FeeRate> coinJoinFeeRateMedians, uint256? roundId = null)
	{
		if (roundId is not null && roundId == LastJudgement.roundId)
		{
			return LastJudgement.judgement;
		}

		var day = TimeSpan.FromHours(24);
		var week = TimeSpan.FromHours(168);
		var month = TimeSpan.FromHours(720);

		var dailyProbability = 1d;
		var weeklyProbability = 1d;
		var monthlyProbability = 1d;

		if (coinJoinFeeRateMedians.TryGetValue(day, out var medianFeeRate))
		{
			// 0.5 satoshi difference is allowable, to avoid rounding errors.
			dailyProbability = roundFeeRate.SatoshiPerByte <= medianFeeRate.SatoshiPerByte + 0.5m ? 1 : Daily;
		}

		if (coinJoinFeeRateMedians.TryGetValue(week, out medianFeeRate))
		{
			// 0.5 satoshi difference is allowable, to avoid rounding errors.
			weeklyProbability = roundFeeRate.SatoshiPerByte <= medianFeeRate.SatoshiPerByte + 0.5m ? 1 : Weekly;
		}

		if (coinJoinFeeRateMedians.TryGetValue(month, out medianFeeRate))
		{
			// 0.5 satoshi difference is allowable, to avoid rounding errors.
			monthlyProbability = roundFeeRate.SatoshiPerByte <= medianFeeRate.SatoshiPerByte + 0.5m ? 1 : Monthly;
		}

		var averageProbabilityPercentage = (int)(100 * (dailyProbability + weeklyProbability + monthlyProbability) / 3d);
		var rand = random.GetInt(1, 101);

		var judgement = averageProbabilityPercentage < rand;
		if (roundId is not null)
		{
			LastJudgement = (roundId, judgement);
		}
		return judgement;
	}

	public override string ToString() => $"{Daily}_{Weekly}_{Monthly}";

	#region Equality

	public override bool Equals(object? obj) => Equals(obj as CoinjoinSkipFactors);

	public bool Equals(CoinjoinSkipFactors? other) => this == other;

	public override int GetHashCode() => HashCode.Combine(Daily, Weekly, Monthly);

	public static bool operator ==(CoinjoinSkipFactors? x, CoinjoinSkipFactors? y)
	{
		if (ReferenceEquals(x, y))
		{
			return true;
		}

		if (x is null || y is null)
		{
			return false;
		}

		if (x.Daily != y.Daily)
		{
			return false;
		}

		if (x.Weekly != y.Weekly)
		{
			return false;
		}

		if (x.Monthly != y.Monthly)
		{
			return false;
		}

		return true;
	}

	public static bool operator !=(CoinjoinSkipFactors? x, CoinjoinSkipFactors? y) => !(x == y);

	#endregion Equality
}
