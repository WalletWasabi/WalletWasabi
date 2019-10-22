using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Tests.NodeBuilding;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BitcoinCore
{
	public class NodeBuildingTests
	{
		[Fact]
		public async Task CanBuildCoreNodeAsync()
		{
			var coreNode = await CoreNode.CreateAsync();
			try
			{
				Assert.False(coreNode.Process.HasExited);
			}
			finally
			{
				await coreNode.StopAsync();
			}
		}

		[Fact]
		public async Task NodesDifferAsync()
		{
			var coreNodes = await Task.WhenAll(CoreNode.CreateAsync(additionalFolder: "0"), CoreNode.CreateAsync(additionalFolder: "1"));
			CoreNode node1 = coreNodes[0];
			CoreNode node2 = coreNodes[1];
			try
			{
				Assert.NotEqual(node1.DataDir, node2.DataDir);
				Assert.NotEqual(node1.Process.Id, node2.Process.Id);
				Assert.NotEqual(node1.P2pEndPoint, node2.P2pEndPoint);
				Assert.NotEqual(node1.RpcEndPoint, node2.RpcEndPoint);
			}
			finally
			{
				await Task.WhenAll(node1.StopAsync(), node2.StopAsync());
			}
		}
	}
}
