using System.Collections.Generic;

namespace WalletWasabi.Blockchain.Analysis.Clustering;

public class LabelsArrayComparer : IComparer<LabelsArray>
{
	private readonly StringComparer _comparer;

	private LabelsArrayComparer(StringComparer comparer)
	{
		_comparer = comparer;
	}

	public static LabelsArrayComparer OrdinalIgnoreCase { get; } = new(StringComparer.OrdinalIgnoreCase);

	public int Compare(LabelsArray x, LabelsArray y) => x.CompareTo(y, _comparer);
}
