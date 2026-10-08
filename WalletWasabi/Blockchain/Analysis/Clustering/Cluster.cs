namespace WalletWasabi.Blockchain.Analysis.Clustering;

public class Cluster(ImmutableHashSet<HdPubKey> keys) : IEquatable<Cluster>
{
	private readonly Lock _lock = new();
	public LabelsArray Labels => LabelsArray.Merge(GetKeys().Select(x => x.Labels));

	private ImmutableHashSet<HdPubKey> _keys = keys;

	private ImmutableHashSet<HdPubKey> GetKeys()
	{
		lock (_lock)
		{
			return _keys;
		}
	}

	public void Merge(Cluster cluster)
	{
		// Variable is used to avoid locking the other cluster, which could lead to a deadlock.
		var otherClusterKeys = cluster.GetKeys();

		lock (_lock)
		{
			_keys = _keys.Union(otherClusterKeys);
		}

		foreach (var key in otherClusterKeys)
		{
			key.Cluster = this;
		}
	}

	public override string ToString() => Labels;

	public override bool Equals(object? obj) => Equals(obj as Cluster);
	public virtual bool Equals(Cluster? other) =>
		other is not null && GetKeys().SetEquals(other.GetKeys());

	/// <remarks>Hash code is computed for a set. Therefore, an order-independent hash function must be used (e.g. XOR).</remarks>
	public override int GetHashCode()
	{
		int hash = 0;

		foreach (var key in GetKeys())
		{
			hash ^= key.GetHashCode();
		}

		return hash;
	}
}
