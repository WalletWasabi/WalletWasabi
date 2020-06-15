using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.CoinJoin.Common.Crypto;
using WalletWasabi.CoinJoin.Common.Models;
using WalletWasabi.Exceptions;
using WalletWasabi.Logging;
using WalletWasabi.WebClients.Wasabi;
using static WalletWasabi.Crypto.SchnorrBlinding;
using UnblindedSignature = WalletWasabi.Crypto.UnblindedSignature;

namespace WalletWasabi.CoinJoin.Client.Clients
{
	public class AliceClient : AliceClientBase
	{
		internal AliceClient(
			long roundId,
			IEnumerable<BitcoinAddress> registeredAddresses,
			IEnumerable<SchnorrPubKey> schnorrPubKeys,
			IEnumerable<Requester> requesters,
			Network network,
			Func<Uri> baseUriAction,
			EndPoint torSocks5EndPoint)
			: base(roundId, registeredAddresses, requesters, network, baseUriAction, torSocks5EndPoint)
		{
			SchnorrPubKeys = schnorrPubKeys.ToArray();
		}

		public SchnorrPubKey[] SchnorrPubKeys { get; }

		protected override PubKey GetSignerPubKey(int i)
		{
			return SchnorrPubKeys[i].SignerPubKey;
		}
	}
}
