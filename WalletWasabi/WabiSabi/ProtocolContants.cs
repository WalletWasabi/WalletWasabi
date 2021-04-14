using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.WabiSabi
{
	public static class ProtocolConstants
	{
		public const int CredentialNumber = 2;
		public const ulong MaxAmountPerAlice = 4_300_000_000_000ul;
		public const uint MaxVsizePerAlice = 255;
	}
}