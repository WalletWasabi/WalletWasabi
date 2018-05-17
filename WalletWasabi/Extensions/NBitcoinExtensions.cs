using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Helpers;
using WalletWasabi.Models;

namespace NBitcoin
{
	public static class NBitcoinExtensions
	{
		public static TxoRef ToTxoRef(this OutPoint me) => new TxoRef(me);

		public static IEnumerable<TxoRef> ToTxoRefs(this TxInList me)
		{
			foreach (var input in me)
			{
				yield return input.PrevOut.ToTxoRef();
			}
		}

		public static IEnumerable<Coin> GetCoins(this TxOutList me, Script script)
		{
			return me.AsCoins().Where(c=>c.ScriptPubKey == script);
		}

		public static string ToHex(this IBitcoinSerializable me)
		{
			return ByteHelpers.ToHex(me.ToBytes());
		}

		public static void FromHex(this IBitcoinSerializable me, string hex)
		{
			Guard.NotNullOrEmptyOrWhitespace(nameof(hex), hex);
			me.FromBytes(ByteHelpers.FromHex(hex));
		}

		public static IEnumerable<(Money value, int count)> GetIndistinguishableOutputs(this Transaction me)
		{
			var ret = new List<(Money Value, int count)>();
			foreach (Money v in me.Outputs.Select(x => x.Value).Distinct())
			{
				ret.Add((v, me.Outputs.Count(x => x.Value == v)));
			}

			return ret;
		}

		public static int GetAnonymitySet(this Transaction me, int outputIndex)
		{
			var output = me.Outputs[outputIndex];
			return me.GetIndistinguishableOutputs().Single(x => x.value == output.Value).count;
		}

		public static int GetMixin(this Transaction me, uint outputIndex)
		{
			var output = me.Outputs[outputIndex];
			return me.GetIndistinguishableOutputs().Single(x => x.value == output.Value).count - 1;
		}
	}
}
