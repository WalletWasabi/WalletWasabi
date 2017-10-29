using HiddenWallet.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NBitcoin
{
    public static class NBitcoinExtensions
    {
        public static ChainedBlock GetBlock(this ConcurrentChain me, Height height)
            => me.GetBlock(height.Value);

		public static string ToHex(this IBitcoinSerializable me)
		{
			return HexHelpers.ToString(me.ToBytes());
		}

		public static void FromHex(this IBitcoinSerializable me, string hex)
		{
			if (me == null) throw new ArgumentNullException(nameof(me));
			me.FromBytes(HexHelpers.GetBytes(hex));
		}

		public static bool VerifyMessage(this BitcoinWitPubKeyAddress me, string message, string signature)
		{
			var key = PubKey.RecoverFromMessage(message, signature);
			return key.Hash == me.Hash;
		}
	}
}
