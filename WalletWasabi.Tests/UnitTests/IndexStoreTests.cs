using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Helpers;
using WalletWasabi.Stores;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class IndexStoreTests
	{
		[Fact]
		public async Task IndexStoreTestsAsync()
		{
			var indexStore = new IndexStore();

			var dir = Path.Combine(Global.Instance.DataDir, EnvironmentHelpers.GetMethodName());
			if (Directory.Exists(dir))
			{
				Directory.Delete(dir, true);
			}
			var network = Network.Main;
			await indexStore.InitializeAsync(dir, network, new SmartHeaderChain());
		}
	}
}
