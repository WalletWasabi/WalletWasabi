using Splat;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Services;

namespace WalletWasabi.Gui
{
	public class Wallet : IComparable<Wallet>
	{
		public Wallet(string walletPath)
		{
			Path = walletPath;
		}

		public async Task LoadWalletAsync ()
		{
			var global = Locator.Current.GetService<Global>();

			KeyManager = global.LoadKeyManager(Path);

			WalletService = await global.CreateWalletServiceAsync(KeyManager);
		}

		public int CompareTo([AllowNull] Wallet other)
		{
			return Path.CompareTo(other.Path);
		}

		public string Path { get; }

        public KeyManager KeyManager { get; private set; }

		public WalletService WalletService { get; private set; }
	}
}
