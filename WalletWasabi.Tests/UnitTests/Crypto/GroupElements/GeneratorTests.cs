using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Crypto.Groups;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto.GroupElements
{
	public class GeneratorTests
	{
		[Fact]
		public void StandardGenerator()
		{
			var generator = new GroupElement(EC.G);
			var generator2 = new GroupElement(new GE(EC.G.x, EC.G.y));
			Assert.Equal(GroupElement.G, generator);
			Assert.Equal(GroupElement.G, generator2);

			Assert.NotEqual(GroupElement.G, GroupElement.Infinity);
			Assert.NotEqual(GroupElement.G, new GroupElement(EC.G * Scalar.Zero));
			Assert.NotEqual(GroupElement.G, new GroupElement(EC.G * new Scalar(2)));

			var infinity = new GroupElement(new GE(EC.G.x, EC.G.y, infinity: true));
			Assert.NotEqual(GroupElement.G, infinity);
			Assert.True(infinity.IsInfinity);
		}

		[Fact]
		public void GeneratorsArentChanged()
		{
			Assert.Equal("0279BE667EF9DCBBAC55A06295CE870B07029BFCDB2DCE28D959F2815B16F81798", ByteHelpers.ToHex(GroupElement.G.ToBytes()));
			Assert.Equal("032F7BF18F3EB9639FF9234D9B74C49E598E34BD3AF1CEEE7378E012E73E66ABAE", ByteHelpers.ToHex(GroupElement.Ga.ToBytes()));
			Assert.Equal("024787C4A880CB54D4A13130E6A31BDEEB047FE9D67C558EAC3DE30CC3A66C277D", ByteHelpers.ToHex(GroupElement.Gg.ToBytes()));
			Assert.Equal("035AF23B3B63E068D2D157513A77CD78BEC1B389EA5EDEC6C9A8DA1254C3DEAC03", ByteHelpers.ToHex(GroupElement.Gh.ToBytes()));
			Assert.Equal("031B786490B66B665F09AE2F848EFC1C2B8D86E35FCDCEF67F61CF5A95FC8C7263", ByteHelpers.ToHex(GroupElement.Gs.ToBytes()));
			Assert.Equal("0322C1EC0F8A3B41D1FB7184692428B0236E253AA643ACCBCF412CAE3231C14967", ByteHelpers.ToHex(GroupElement.GV.ToBytes()));
			Assert.Equal("0338A4AAE54CCB98207FA4707DB4F711A9427B0444F97FC8F54EF9D00E25013C2F", ByteHelpers.ToHex(GroupElement.Gw.ToBytes()));
			Assert.Equal("02A456261F53BFC99BF0DB0D71FE3E1A259961D9DA896217DAC475841DA3A4F16C", ByteHelpers.ToHex(GroupElement.Gwp.ToBytes()));
			Assert.Equal("0281E7BCF60042263290325032954C500DCCE6D470A952C2FCDC62CF2280282D76", ByteHelpers.ToHex(GroupElement.Gx0.ToBytes()));
			Assert.Equal("03A031FE97954902E3E84241D5E48264F06748FA741A9B2C35E8DA7C772C1A5105", ByteHelpers.ToHex(GroupElement.Gx1.ToBytes()));
		}
	}
}
