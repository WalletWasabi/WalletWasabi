using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.WabiSabi.Crypto;

namespace WalletWasabi.Tests.Helpers
{
	public static class CredentialPoolExtensions
	{
		public static Credential[] Take(this CredentialPool pool, long value)
			=> pool.TakeAsync(value).GetAwaiter().GetResult();
	}
}
