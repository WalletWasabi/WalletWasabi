using NBitcoin;

namespace WalletWasabi.Affiliation.Models.CoinjoinRequest;

public record Outpoint(byte[] Hash, long Index)
{
	public static Outpoint FromOutPoint(OutPoint outPoint) =>
		new (outPoint.Hash.ToBytes(), outPoint.N);
}
