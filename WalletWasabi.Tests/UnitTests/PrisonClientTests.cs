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

namespace WalletWasabi.Tests.UnitTests;

public class PrisonClientTests
{
	[Fact]
	private void CanCreateAndLoadPrisonClientDataTest()
	{
		var workDir = Common.GetWorkDir();
		PrisonClient pc = PrisonClient.CreateOrLoadFromFile(workDir);
		Assert.Empty(pc.PrisonedCoins);

		var km = KeyManager.CreateNew(out _, "", Network.Main);
		for (int i = 0; i < 5; i++)
		{
			var hpk = BitcoinFactory.CreateHdPubKey(km);
			var sc = BitcoinFactory.CreateSmartCoin(hpk, 1m);
			pc.TryAddCoin(sc, DateTimeOffset.UtcNow);
		}
		pc = PrisonClient.CreateOrLoadFromFile(workDir);
		Assert.NotNull(pc.FilePath);
		Assert.Equal(5, pc.PrisonedCoins.Count);
		File.Delete(pc.FilePath);
	}
}
