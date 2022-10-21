using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.Affiliation.Serialization;

namespace WalletWasabi.Affiliation.Models.CoinjoinRequest;

public record Outpoint
{
	public Outpoint(byte[] hash, long index)
	{
		Hash = hash;
		Index = index;
	}

	[JsonProperty(PropertyName = "hash")]
	[JsonConverter(typeof(AffiliationByteArrayJsonConverter))]
	public byte[] Hash { get; }

	[JsonProperty(PropertyName = "index")]
	public long Index { get; }

	public static Outpoint FromOutPoint(OutPoint outPoint)
	{
		return new Outpoint(outPoint.Hash.ToBytes(lendian: true), outPoint.N);
	}
}
