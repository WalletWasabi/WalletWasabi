using NBitcoin;

namespace WalletWasabi.Affiliation.Extensions;

public static class NBitcoinExtensions
{
	public static long ToSlip44CoinType(this Network me) => 
		me.Name switch
		{
			"Main" => 0,
			"RegTest" => 1,
			"TestNet" => 1,
			_ => throw new NotImplementedException()
		};
}
