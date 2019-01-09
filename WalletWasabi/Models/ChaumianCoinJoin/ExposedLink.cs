using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;
using WalletWasabi.KeyManagement;

namespace WalletWasabi.Models.ChaumianCoinJoin
{
	[JsonObject(MemberSerialization.OptIn)]
	public class ExposedLink
	{
		[JsonProperty]
		public IEnumerable<TxoRef> Inputs { get; set; }

		[JsonProperty]
		[JsonConverter(typeof(BitcoinAddressJsonConverter))]
		public BitcoinAddress Change { get; set; }

		[JsonProperty]
		public IEnumerable<BitcoinAddressBlindedPair> Actives { get; set; }

		[JsonConstructor]
		public ExposedLink(IEnumerable<TxoRef> inputs, BitcoinAddress change, IEnumerable<BitcoinAddress> actives)
		{
			Create(inputs, change, actives);
		}

		public ExposedLink(IEnumerable<TxoRef> inputs, HdPubKey change, IEnumerable<HdPubKey> actives, Network network)
		{
			Guard.NotNull(nameof(network), network);
			Guard.NotNull(nameof(change), change);
			Guard.NotNullOrEmpty(nameof(actives), actives);

			Create(inputs, change.GetP2wpkhAddress(network), actives.Select(x => x.GetP2wpkhAddress(network) as BitcoinAddress));
		}

		private void Create(IEnumerable<TxoRef> inputs, BitcoinAddress change, IEnumerable<BitcoinAddress> actives)
		{
			Inputs = Guard.NotNullOrEmpty(nameof(inputs), inputs);
			Change = Guard.NotNull(nameof(change), change);

			var activeList = new List<BitcoinAddressBlindedPair>();
			Guard.NotNullOrEmpty(nameof(actives), actives);
			foreach (BitcoinAddress active in actives)
			{
				activeList.Add(new BitcoinAddressBlindedPair(active, true));
			}
			Actives = activeList;
		}
	}
}
