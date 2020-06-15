using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.CoinJoin.Common.Crypto;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.CoinJoin.Common.Models
{
	public class RoundStateResponse : RoundStateResponseBase
	{
		public IEnumerable<SchnorrPubKey> SchnorrPubKeys { get; set; }

		public override int MixLevelCount => SchnorrPubKeys.Count();
	}
}
