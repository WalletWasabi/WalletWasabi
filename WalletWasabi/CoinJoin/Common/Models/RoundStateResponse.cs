using System.Collections.Generic;
using System.Linq;
using WalletWasabi.CoinJoin.Common.Crypto;

namespace WalletWasabi.CoinJoin.Common.Models
{
	public class RoundStateResponse : RoundStateResponseBase
	{
		public IEnumerable<SchnorrPubKey> SchnorrPubKeys { get; set; }

		public override int MixLevelCount => SchnorrPubKeys.Count();
	}
}
