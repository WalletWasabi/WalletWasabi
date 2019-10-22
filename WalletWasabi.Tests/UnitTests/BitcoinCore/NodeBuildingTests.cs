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
		public async Task DataDirDiffersAsync()
		{
			var coreNodes = await Task.WhenAll(CoreNode.CreateAsync(additionalFolder: "0"), CoreNode.CreateAsync(additionalFolder: "1"));
			try
			{
				Assert.NotEqual(coreNodes[0].DataDir, coreNodes[1].DataDir);
			}
			finally
			{
				await Task.WhenAll(coreNodes[0].StopAsync(), coreNodes[1].StopAsync());
			}
		}
	}
}
