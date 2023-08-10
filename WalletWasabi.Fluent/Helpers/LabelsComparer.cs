using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.Analysis.Clustering;

namespace WalletWasabi.Fluent.Helpers;

public class LabelsComparer : IEqualityComparer<LabelsArray>
{
	private static LabelsComparer? ComparerInstance;

	public static IEqualityComparer<LabelsArray> Instance => ComparerInstance ??= new LabelsComparer();

	public bool Equals(LabelsArray x, LabelsArray y)
	{
		if (x.GetType() != y.GetType())
		{
			return false;
		}

		return x.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(y.ToHashSet(StringComparer.OrdinalIgnoreCase));
	}

	public int GetHashCode(LabelsArray obj)
	{
		return 0;
	}
}
