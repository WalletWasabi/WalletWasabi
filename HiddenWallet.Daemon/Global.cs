using HiddenWallet.Daemon.Wrappers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using HiddenWallet.Crypto;

namespace HiddenWallet.Daemon
{
    public static class Global
	{
		public static WalletWrapper WalletWrapper;
		public static Config Config;
		public static BlindingRsaPubKey RsaPubKey;
	}
}
