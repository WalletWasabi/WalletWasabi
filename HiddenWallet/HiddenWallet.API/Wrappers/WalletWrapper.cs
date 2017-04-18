using HBitcoin.KeyManagement;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.API.Wrappers
{
	public class WalletWrapper
	{
		private string _password = null;

		public WalletWrapper()
		{
			// Loads the config file
			// It also creates it with default settings if doesn't exist
			Config.Load();
		}

		public string Create(string password)
		{
			Safe.Create(out Mnemonic mnemonic, password, Config.WalletFilePath, Config.Network);
			return mnemonic.ToString();
		}

		public void Load(string password)
		{
			Safe safe = Safe.Load(password, Config.WalletFilePath);
			if (safe.Network != Config.Network) throw new NotSupportedException("Network in the config file differs from the netwrok in the wallet file");
			_password = password;

			//todo start syncing here
		}

		public void Recover(string password, string mnemonic, string creationTime)
		{
			Safe.Recover(
				new Mnemonic(mnemonic), 
				password, 
				Config.WalletFilePath, 
				Config.Network, 
				DateTimeOffset.ParseExact(creationTime, "yyyy-MM-dd", CultureInfo.InvariantCulture));
		}
	}
}
