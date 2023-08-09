using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.Analysis.Clustering;

namespace WalletWasabi.Fluent.Helpers;

public class LabelsComparer : IEqualityComparer<LabelsArray>
{
	public bool Equals(LabelsArray x, LabelsArray y)
	{
		if (x.GetType() != y.GetType())
		{
			return false;
		}

		return x.SequenceEqual(y, comparer: StringComparer.InvariantCultureIgnoreCase);
	}

	public int GetHashCode(LabelsArray obj)
	{
		return obj.GetHashCode();
	}
}
