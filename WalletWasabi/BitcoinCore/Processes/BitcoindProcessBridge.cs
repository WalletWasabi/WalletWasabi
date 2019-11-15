using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Microservices;

namespace WalletWasabi.BitcoinCore.Processes
{
	public class BitcoindProcessBridge : ProcessBridge
	{
		public BitcoindProcessBridge() : base(MicroserviceHelpers.GetBinaryPath("bitcoind"))
		{
		}
	}
}
