using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Stores.Filters;
using Xunit;

namespace WalletWasabi.Tests.StoreTests
{
	public class IndexStoreTests
	{
		[Fact]
		public async Task CanInitializeIndexStoreAsync()
		{
			var indexStore = new IndexStore();

			var dir = Path.Combine(Global.Instance.DataDir, EnvironmentHelpers.GetMethodName());
			var network = Network.Main;
			await indexStore.InitializeAsync(dir, network, false);

			Assert.Equal(network, indexStore.Network);
			Assert.Equal(dir, indexStore.WorkFolderPath);
		}
	}
}
