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
		// Read the other cluster's keys before taking our lock so the two locks are never nested.
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
	public bool Equals(Cluster? other)
	{
		if (ReferenceEquals(this, other))
		{
			return true;
		}

		if (other is null)
		{
			return false;
		}

		// Not `SetEquals`: on .NET 10 it copies the other set into a new `HashSet` on every call.
		var mine = GetKeys();
		var theirs = other.GetKeys();
		return mine.Count == theirs.Count && mine.IsSupersetOf(theirs);
	}

	public static bool operator ==(Cluster? x, Cluster? y) => x?.Equals(y) ?? y is null;

	public static bool operator !=(Cluster? x, Cluster? y) => !(x == y);

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
