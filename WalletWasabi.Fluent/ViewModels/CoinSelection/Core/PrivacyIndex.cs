using System.Linq;
using WalletWasabi.Blockchain.Analysis.Clustering;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core;

public class PrivacyIndex : IComparable<PrivacyIndex>
{
	private PrivacyIndex(SmartLabel labels, PrivacyLevel privacyLevel)
	{
		Labels = labels;
		PrivacyLevel = privacyLevel;
	}

	public SmartLabel Labels { get; }
	public PrivacyLevel PrivacyLevel { get; }

	public int CompareTo(PrivacyIndex? key)
	{
		return GetScore(this).CompareTo(GetScore(key));
	}

	public static PrivacyIndex Get(SmartLabel labels, PrivacyLevel privacyLevel)
	{
		return new PrivacyIndex(labels, privacyLevel);
	}

	public override bool Equals(object? obj)
	{
		if (obj is null)
		{
			return false;
		}

		if (ReferenceEquals(this, obj))
		{
			return true;
		}

		if (obj.GetType() != GetType())
		{
			return false;
		}

		return Equals((PrivacyIndex) obj);
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(Labels, (int) PrivacyLevel);
	}

	protected bool Equals(PrivacyIndex other)
	{
		if (Labels.IsEmpty && other.Labels.IsEmpty)
		{
			return PrivacyLevel == other.PrivacyLevel;
		}

		return Labels.Equals(other.Labels);
	}

	private static int GetScore(PrivacyIndex? key)
	{
		if (key is null || key.PrivacyLevel == PrivacyLevel.None)
		{
			return int.MinValue;
		}

		if (key.PrivacyLevel == PrivacyLevel.Private)
		{
			return 2;
		}

		if (key.PrivacyLevel == PrivacyLevel.SemiPrivate)
		{
			return 1;
		}

		return -key.Labels.Count();
	}
}
