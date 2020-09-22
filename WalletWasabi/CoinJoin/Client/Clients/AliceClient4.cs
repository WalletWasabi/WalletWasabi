using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using static WalletWasabi.Crypto.SchnorrBlinding;

namespace WalletWasabi.CoinJoin.Client.Clients
{
	public class AliceClient4 : AliceClientBase
	{
		internal AliceClient4(
			long roundId,
			IEnumerable<BitcoinAddress> registeredAddresses,
			IEnumerable<PubKey> signerPubKeys,
			IEnumerable<Requester> requesters,
			Network network,
			Func<Uri> baseUriAction,
			EndPoint torSocks5EndPoint)
			: base(roundId, registeredAddresses, requesters, network, baseUriAction, torSocks5EndPoint)
		{
			SignerPubKeys = signerPubKeys.ToArray();
		}

		public PubKey[] SignerPubKeys { get; }

		protected override PubKey GetSignerPubKey(int i)
		{
			return SignerPubKeys[i];
		}
	}
}
