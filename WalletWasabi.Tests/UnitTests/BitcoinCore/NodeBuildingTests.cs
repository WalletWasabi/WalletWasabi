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
	}
}
