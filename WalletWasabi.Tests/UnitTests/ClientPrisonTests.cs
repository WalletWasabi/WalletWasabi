using NBitcoin;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Client.Banning;
using Xunit;
using System.Threading;

namespace WalletWasabi.Tests.UnitTests;

public class ClientPrisonTests
{
	[Fact]
	public async Task CanCreateAndLoadPrisonClientDataTestAsync()
	{
		var workDir = Common.GetWorkDir();
		ClientPrison cp = ClientPrison.CreateOrLoadFromFile(workDir);

		var km = KeyManager.CreateNew(out _, "", Network.Main);
		for (int i = 0; i < 5; i++)
		{
			var hpk = BitcoinFactory.CreateHdPubKey(km);
			var sc = BitcoinFactory.CreateSmartCoin(hpk, 1m);
			cp.AddCoin(sc, DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5));
		}
		Assert.Equal(5, cp.PrisonedCoins.Count);

		// Call StartAsync to write to file
		await cp.StartAsync(CancellationToken.None);
		cp.Dispose();

		cp = ClientPrison.CreateOrLoadFromFile(workDir);
		Assert.Equal(5, cp.PrisonedCoins.Count);
		await Task.Delay(5000);

		// Call StartAsync to remove expired coins
		await cp.StartAsync(CancellationToken.None);
		Assert.Empty(cp.PrisonedCoins);

		File.Delete(cp.FilePath);
		cp.Dispose();
	}
}
