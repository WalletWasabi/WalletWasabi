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
			return me.AsCoins().Where(c => c.ScriptPubKey == script);
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

		/// <summary>
		/// Based on transaction data, it decides if it's possible that native segwit script played a par in this transaction.
		/// </summary>
		public static bool PossiblyNativeSegWitInvolved(this Transaction me)
		{
			// We omit Guard, because it's performance critical in Wasabi.
			// We start with the inputs, because, this check is faster.
			// Note: by testing performance the order doesn't seem to affect the speed of loading the wallet.
			foreach (TxIn input in me.Inputs)
			{
				if (input.ScriptSig is null || input.ScriptSig == Script.Empty)
				{
					return true;
				}
			}
			foreach (TxOut output in me.Outputs)
			{
				if (output.ScriptPubKey.IsWitness)
				{
					return true;
				}
			}
			return false;
		}

		public static IEnumerable<(Money value, int count)> GetIndistinguishableOutputs(this Transaction me)
		{
			return me.Outputs.GroupBy(x => x.Value)
			   .ToDictionary(x => x.Key, y => y.Count())
			   .Select(x => (x.Key, x.Value));
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

		/// <summary>
		/// Careful, if it's in a legacy block then this won't work.
		/// </summary>
		public static bool HasWitScript(this TxIn me)
		{
			Guard.NotNull(nameof(me), me);

			bool notNull = !(me.WitScript is null);
			bool notEmpty = me.WitScript != WitScript.Empty;
			return notNull && notEmpty;
		}

		public static Money Percentange(this Money me, decimal perc)
		{
			return Money.Satoshis((me.Satoshi / 100m) * perc);
		}

		public static decimal ToUsd(this Money me, decimal btcExchangeRate)
		{
			return me.ToDecimal(MoneyUnit.BTC) * btcExchangeRate;
		}
	}
}
