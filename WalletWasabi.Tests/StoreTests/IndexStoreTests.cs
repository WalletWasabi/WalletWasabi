using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
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

			var dir = Path.Combine(Global.Instance.DataDir, nameof(CanInitializeIndexStoreAsync));
			var network = Network.Main;
			await indexStore.InitializeAsync(dir, network, new HashChain());

			Assert.Equal(network, indexStore.Network);
			Assert.Equal(dir, indexStore.WorkFolderPath);
		}
	}
}
