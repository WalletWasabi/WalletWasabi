using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
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

			var dir = Path.Combine(Global.Instance.DataDir, nameof(CanInitializeMempoolStore));
			var network = Network.Main;
			mempoolStore.Initialize(dir, network);

			Assert.Equal(network, mempoolStore.Network);
			Assert.Equal(dir, mempoolStore.WorkFolderPath);
		}
	}
}
