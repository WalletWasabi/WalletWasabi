using WalletWasabi.Blockchain.Analysis.Clustering;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core;

public class GroupKey
{
	public GroupKey(SmartLabel labels, PrivacyLevel privacyLevel)
	{
		Labels = labels;
		PrivacyLevel = privacyLevel;
	}

	public SmartLabel Labels { get; }
	public PrivacyLevel PrivacyLevel { get; }

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

		return Equals((GroupKey) obj);
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(Labels, (int) PrivacyLevel);
	}

	protected bool Equals(GroupKey other)
	{
		if (Labels.IsEmpty && other.Labels.IsEmpty)
		{
			return PrivacyLevel == other.PrivacyLevel;
		}

		return Labels.Equals(other.Labels);
	}
}
