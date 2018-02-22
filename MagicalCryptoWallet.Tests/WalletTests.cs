using MagicalCryptoWallet.KeyManagement;
using MagicalCryptoWallet.Services;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace MagicalCryptoWallet.Tests
{
    public class WalletTests : IClassFixture<SharedFixture>
	{
		private SharedFixture SharedFixture { get; }

		public WalletTests(SharedFixture fixture)
		{
			SharedFixture = fixture;
		}

		[Fact]
		public async Task BasicWalletTestAsync()
		{
			var manager = KeyManager.CreateNew(out Mnemonic mnemonic, "password");
			var dataFolder = Path.Combine(SharedFixture.DataDir, nameof(BasicWalletTestAsync));
			using (var wallet = new WalletService(dataFolder, Network.TestNet, manager))
			{
				wallet.Start();
				await Task.Delay(1000);
			}
		}
	}
}
