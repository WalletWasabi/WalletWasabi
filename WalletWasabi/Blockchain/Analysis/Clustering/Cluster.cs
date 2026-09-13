namespace WalletWasabi.Blockchain.Analysis.Clustering;

public record Cluster(IImmutableSet<HdPubKey> Keys)
{
	private readonly Lock _lock = new();
	public LabelsArray Labels => LabelsArray.Merge(KeysSet.Select(x => x.Labels));

	private IImmutableSet<HdPubKey> KeysSet
	{
		get
		{
			lock (_lock)
			{
				return field;
			}
		}

		set
		{
			lock (_lock)
			{
				field = value;
			}
		}
	} = Keys;

	public void Merge(Cluster cluster)
	{
		KeysSet = KeysSet.Union(cluster.KeysSet);

		foreach (var key in cluster.KeysSet)
		{
			key.Cluster = this;
		}
	}

	public override string ToString() => Labels;

	public virtual bool Equals(Cluster? other) =>
		other is not null && KeysSet.SetEquals(other.KeysSet);

	/// <remarks>Hash code is computed for a set. Therefore, an order-independent hash function must be used (e.g. XOR).</remarks>
	public override int GetHashCode()
	{
		int hash = 0;

		foreach (var key in KeysSet)
		{
			hash ^= key.GetHashCode();
		}

		return hash;
	}
}
