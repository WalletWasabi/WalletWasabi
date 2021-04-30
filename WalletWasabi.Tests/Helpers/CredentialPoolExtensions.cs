using NBitcoin;
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
		public static Credential[] GetCredentialsForRequester(this CredentialPool pool, uint256 requesterId)
			=> pool.GetCredentialsForRequesterAsync(requesterId).GetAwaiter().GetResult();
	}
}
