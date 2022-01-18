using WalletWasabi.Blockchain.Blocks;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Blockchain.Blocks;

public class SmarthHeaderChainTests
{
	[Fact]
	public void GeneralHashChainTests()
	{
		SmartHeaderChain hashChain = new();

		AssertEverythingDefault(hashChain);

		// Attempt to remove an element when there is none.
		Assert.False(hashChain.RemoveLast());
		AssertEverythingDefault(hashChain);

		uint newServerHeight = hashChain.ServerTipHeight + 1;
		hashChain.UpdateServerTipHeight(newServerHeight);

		newServerHeight++;
		hashChain.UpdateServerTipHeight(newServerHeight);

		newServerHeight++;
		hashChain.UpdateServerTipHeight(newServerHeight);

		newServerHeight++;
		hashChain.UpdateServerTipHeight(newServerHeight);

		newServerHeight++;
		hashChain.UpdateServerTipHeight(newServerHeight);

		uint sameServerheight = newServerHeight;
		hashChain.UpdateServerTipHeight(sameServerheight);

		hashChain.UpdateServerTipHeight(sameServerheight);

		// ASSERT PROPERTIES
		Assert.Equal(0, hashChain.HashCount);
		uint hashesLeft = sameServerheight;
		Assert.Equal((int)hashesLeft, hashChain.HashesLeft);
		Assert.Equal(hashesLeft, hashChain.ServerTipHeight);
		Assert.Null(hashChain.TipHash);
		Assert.Equal(0u, hashChain.TipHeight);
	}

	private static void AssertEverythingDefault(SmartHeaderChain hashChain)
	{
		Assert.Equal(0, hashChain.HashCount);
		Assert.Equal(0, hashChain.HashesLeft);
		Assert.Equal(0u, hashChain.ServerTipHeight);
		Assert.Null(hashChain.TipHash);
		Assert.Equal(0u, hashChain.TipHeight);
	}
}
