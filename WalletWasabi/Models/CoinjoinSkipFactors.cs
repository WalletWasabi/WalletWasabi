using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;

namespace WalletWasabi.Models;

public class CoinjoinSkipFactors : IEquatable<CoinjoinSkipFactors>
{
	public CoinjoinSkipFactors(double daily, double weekly, double monthly)
	{
		Daily = daily;
		Weekly = weekly;
		Monthly = monthly;
	}

	public static CoinjoinSkipFactors NoSkip => new(1, 1, 1);
	public static CoinjoinSkipFactors SpeedMaximizing => new(0.7, 0.8, 0.9);
	public static CoinjoinSkipFactors CostMinimizing => new(0.1, 0.2, 0.3);
	public static CoinjoinSkipFactors PrivacyMaximizing => new(0.5, 0.5, 0.5);

	public static CoinjoinSkipFactors FromString(string str)
	{
		var parts = str.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		var factors = new CoinjoinSkipFactors(double.Parse(parts[0]), double.Parse(parts[1]), double.Parse(parts[2]));
		return factors;
	}

	public double Daily { get; }
	public double Weekly { get; }
	public double Monthly { get; }

	public override string ToString() => $"{Daily}-{Weekly}-{Monthly}";

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
