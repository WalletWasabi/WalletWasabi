using NBitcoin;
using WalletWasabi.Crypto;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto;

public class Slip21Tests
{
	[Fact]
	public void TestVectors()
	{
		var allMnemonic = new Mnemonic("all all all all all all all all all all all all");

		var master = Slip21Node.FromSeed(allMnemonic.DeriveSeed());
		Assert.Equal("dbf12b44133eaab506a740f6565cc117228cbf1dd70635cfa8ddfdc9af734756", master.Key.ToHex());

		var child1 = master.DeriveChild("SLIP-0021");
		Assert.Equal("1d065e3ac1bbe5c7fad32cf2305f7d709dc070d672044a19e610c77cdf33de0d", child1.Key.ToHex());

		var child2 = child1.DeriveChild("Master encryption key");
		Assert.Equal("ea163130e35bbafdf5ddee97a17b39cef2be4b4f390180d65b54cf05c6a82fde", child2.Key.ToHex());

		var child3 = child1.DeriveChild("Authentication key");
		Assert.Equal("47194e938ab24cc82bfa25f6486ed54bebe79c40ae2a5a32ea6db294d81861a6", child3.Key.ToHex());
	}
}
