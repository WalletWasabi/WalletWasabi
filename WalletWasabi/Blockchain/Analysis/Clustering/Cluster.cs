using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.Blockchain.Analysis.Clustering;

public class Cluster(IEnumerable<HdPubKey> keys) : IEquatable<Cluster>
{
	public LabelsArray Labels => LabelsArray.Merge(KeysSet.Select(x => x.Labels));

	private readonly object _lock = new();
	private HashSet<HdPubKey> KeysSet { get; } = keys.ToHashSet();

	public void Merge(Cluster cluster) => Merge(cluster.KeysSet);

	private void Merge(IEnumerable<HdPubKey> keys)
	{
		lock (_lock)
		{
			foreach (var key in keys.ToList())
			{
				KeysSet.Add(key);
				key.Cluster = this;
			}
		}
	}

	public override string ToString() => Labels;

	#region EqualityAndComparison

	public override bool Equals(object? obj) => Equals(obj as Cluster);

	public bool Equals(Cluster? other) => this == other;

	public override int GetHashCode()
	{
		lock (_lock)
		{
			int hash = 0;
			foreach (var key in KeysSet)
			{
				hash ^= key.GetHashCode();
			}
			return hash;
		}
	}

	public static bool operator ==(Cluster? x, Cluster? y)
	{
		if (ReferenceEquals(x, y))
		{
			return true;
		}

		if (x is null || y is null)
		{
			return false;
		}

		lock (x._lock)
		{
			lock (y._lock)
			{
				// We lose the order here, which isn't great and may cause problems,
				// but this is also a significant performance gain.
				return x.KeysSet.SetEquals(y.KeysSet);
			}
		}
	}

	public static bool operator !=(Cluster? x, Cluster? y) => !(x == y);

	#endregion EqualityAndComparison
}
