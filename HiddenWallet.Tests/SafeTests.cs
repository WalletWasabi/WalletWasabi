using System;
using System.Globalization;
using HiddenWallet.KeyManagement;
using NBitcoin;
using Xunit;
using HiddenWallet.Models;
using System.Threading.Tasks;
using System.IO;

namespace HiddenWallet.Tests
{
	public class SafeTests
	{
		[Fact]
		public async Task CreationTestsAsync()
		{
			for (int i = 0; i < 2; i++)
			{
				var network = i == 0 ? Network.Main : Network.TestNet;

                const string path = "Wallets/TestWallet.json";
                const string password = "password";
				const string recoveredPath = "Wallets/RecoveredTestWallet.json";

				if (File.Exists(path)) File.Delete(path);
				if (File.Exists(recoveredPath)) File.Delete(recoveredPath);

				var result = await Safe.CreateAsync(password, path, network);
                var safe = result.Safe;
                var mnemonic = result.Mnemonic;
				var loadedSafe = await Safe.LoadAsync(password, path);

				var wantedCreation = DateTimeOffset.ParseExact("1998-01-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
				var recoverdSafe = await Safe.RecoverAsync(mnemonic, password, recoveredPath, network, wantedCreation);

				var alice = new SafeAccount(3);
				var bob = new SafeAccount(4);
				try
				{
                    foreach (AddressType addressType in Enum.GetValues(typeof(AddressType)))
                    {
                        Assert.Equal(safe.GetAddress(addressType, 3, account: alice), recoverdSafe.GetAddress(addressType, 3, account: alice));
                        Assert.Equal(safe.GetAddress(addressType, 4, HdPathType.NonHardened, account: alice), recoverdSafe.GetAddress(addressType, 4, HdPathType.NonHardened, account: alice));
                        Assert.NotEqual(safe.GetAddress(addressType, 4, HdPathType.Change, account: alice), recoverdSafe.GetAddress(addressType, 4, HdPathType.NonHardened, account: alice));
                        Assert.NotEqual(safe.GetAddress(addressType, 3, HdPathType.NonHardened, account: alice), recoverdSafe.GetAddress(addressType, 4, HdPathType.NonHardened, account: alice));
                        Assert.NotEqual(safe.GetAddress(addressType, 4, HdPathType.NonHardened, account: alice), recoverdSafe.GetAddress(addressType, 4, HdPathType.NonHardened, account: bob));
                        Assert.NotEqual(safe.GetAddress(addressType, 4, account: alice), safe.GetAddress(addressType, 4));
                    }

					Assert.Equal(DateTimeOffset.UtcNow.Date, safe.CreationTime.Date);
					Assert.InRange(safe.CreationTime, Safe.EarliestPossibleCreationTime, DateTimeOffset.UtcNow);
					Assert.True(wantedCreation < recoverdSafe.CreationTime);
					Assert.Equal(network, safe.Network);
					Assert.Equal(network, loadedSafe.Network);
					Assert.Equal(network, recoverdSafe.Network);
				}
				finally
				{
					safe?.Delete();
					recoverdSafe?.Delete();
				}
			}
		}

		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		[InlineData(100)]
		[InlineData(9999)]
		public async Task ProperlyLoadRecoverAsync(int index)
		{
			Network network = Network.TestNet;
            const string path = "Wallets/TestWallet2.json";
            const string password = "password";

			var result = await Safe.CreateAsync(password, path, network);
            var safe = result.Safe;
            var mnemonic = result.Mnemonic;
			var loadedSafe = await Safe.LoadAsync(password, path);
			var recoverdSafe = await Safe.RecoverAsync(mnemonic, password, "Wallets/RecoveredTestWallet.json", network, Safe.EarliestPossibleCreationTime);

			try
			{
				Assert.Equal(safe.ExtKey.ScriptPubKey, loadedSafe.ExtKey.ScriptPubKey);
				Assert.Equal(safe.ExtKey.ScriptPubKey, loadedSafe.ExtKey.ScriptPubKey);
				Assert.Equal(loadedSafe.BitcoinExtKey, recoverdSafe.BitcoinExtKey);
				Assert.Equal(loadedSafe.GetBitcoinExtPubKey(index: null, hdPathType: HdPathType.NonHardened, account: new SafeAccount(1)), recoverdSafe.GetBitcoinExtPubKey(index: null, hdPathType: HdPathType.NonHardened, account: new SafeAccount(1)));
                foreach (AddressType addressType in Enum.GetValues(typeof(AddressType)))
                {
                    Assert.Equal(loadedSafe.GetAddress(addressType, index), recoverdSafe.GetAddress(addressType, index));
                }
				Assert.Equal(loadedSafe.GetBitcoinExtKey(index), recoverdSafe.GetBitcoinExtKey(index));
			}
			finally
			{
				safe?.Delete();
				recoverdSafe?.Delete();
			}
		}
	}
}
