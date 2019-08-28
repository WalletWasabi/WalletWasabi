using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Stores.Mempool;
using Xunit;

namespace WalletWasabi.Tests.StoreTests
{
	public class MempoolTests
	{
		[Fact]
		public void CanInitializeMempoolStore()
		{
			var mempoolStore = new MempoolStore();

			var network = Network.Main;
			mempoolStore.Initialize(network);

			Assert.Equal(network, mempoolStore.Network);
		}
	}
}
