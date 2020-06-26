using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Helpers;
using WalletWasabi.Stores;

namespace WalletWasabi.Blockchain.Transactions
{
	public class TransactionFactoryConfiguration
	{
		/// <param name="allowUnconfirmed">Allow to spend unconfirmed transactions, if necessary.</param>
		public TransactionFactoryConfiguration(Network network, KeyManager keyManager, ICoinsView coins, BitcoinStore store, string password = "", bool allowUnconfirmed = false)
		{
			Network = Guard.NotNull(nameof(network), network);
			KeyManager = Guard.NotNull(nameof(keyManager), keyManager);
			Coins = Guard.NotNull(nameof(coins), coins);
			Store = Guard.NotNull(nameof(store), store);
			Password = password;
			AllowUnconfirmed = allowUnconfirmed;
		}

		public Network Network { get; }
		public KeyManager KeyManager { get; }
		public ICoinsView Coins { get; }
		public BitcoinStore Store { get; }
		public string Password { get; }
		public bool AllowUnconfirmed { get; }
	}
}
