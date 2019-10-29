using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.Logging;
using WalletWasabi.Microservices;

namespace WalletWasabi.BitcoinCore
{
	public class BitcoindProcessBridge : ProcessBridge
	{
		public BitcoindProcessBridge() : base(EnvironmentHelpers.GetBinaryPath("bitcoind"))
		{
		}
	}
}
