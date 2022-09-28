using NBitcoin.Secp256k1;
using WalletWasabi.Crypto.Groups;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto.GroupElements;

public class GeneratorTests
{
	[Fact]
	public void StandardGenerator()
	{
		var generator = new GroupElement(EC.G);
		var generator2 = new GroupElement(new GE(EC.G.x, EC.G.y));
		Assert.Equal(Generators.G, generator);
		Assert.Equal(Generators.G, generator2);

		Assert.NotEqual(Generators.G, GroupElement.Infinity);
		Assert.NotEqual(Generators.G, new GroupElement(EC.G) * Scalar.Zero);
		Assert.NotEqual(Generators.G, new GroupElement(EC.G) * new Scalar(2));

		var infinity = new GroupElement(new GE(EC.G.x, EC.G.y, infinity: true));
		Assert.NotEqual(Generators.G, infinity);
		Assert.True(infinity.IsInfinity);
	}

	[Fact]
	public void GeneratorsArentChanged()
	{
		Assert.Equal("0279BE667EF9DCBBAC55A06295CE870B07029BFCDB2DCE28D959F2815B16F81798", ByteHelpers.ToHex(Generators.G.ToBytes()));
		Assert.Equal("03AB8F46084B4FA0FC8261328A5A71AF267B1D1F8FE229C63C751D02A2E996E0EC", ByteHelpers.ToHex(Generators.Ga.ToBytes()));
		Assert.Equal("02FB8868ACD9CBBD68964BAA1CFA6B893A6269E01569183474E6C1C4242A0071A9", ByteHelpers.ToHex(Generators.Gg.ToBytes()));
		Assert.Equal("023D11E10CE7A8C17671ED777886FC2B84E65A532FA0C411ABBE96E1206F9DFF80", ByteHelpers.ToHex(Generators.Gh.ToBytes()));
		Assert.Equal("031E7775ED62B79E9E83366198CFE69DFE7408AFF10C331CEE3B2C7F7A5F2EB0C8", ByteHelpers.ToHex(Generators.Gs.ToBytes()));
		Assert.Equal("03665E9B8468DCEDA16ED3E315FBD0A0E597F4AA3B4C6F2146437F53F3AF204C2C", ByteHelpers.ToHex(Generators.GV.ToBytes()));
		Assert.Equal("02B4DF49B623A8A0B245CCF2867134A5DAC12FE39ECEC08B3D361801D2C79DDC14", ByteHelpers.ToHex(Generators.Gw.ToBytes()));
		Assert.Equal("03F50265578FCE5E977162E662ED75D7224AE720FA79B72CF2B6FB86B2136E3B48", ByteHelpers.ToHex(Generators.Gwp.ToBytes()));
		Assert.Equal("02E33C9F3CBE6388A2D3C3ECB12153DB73499928541905D86AAA4FFC01F2763B54", ByteHelpers.ToHex(Generators.Gx0.ToBytes()));
		Assert.Equal("0246253CC926AAB789BAA278AB9A54EDEF455CA2014038E9F84DE312C05A8121CC", ByteHelpers.ToHex(Generators.Gx1.ToBytes()));
	}

	[Fact]
	public void FriendlyNameNullCheck()
	{
		Assert.False(Generators.TryGetFriendlyGeneratorName(null, out var name));
		Assert.NotNull(name);
		Assert.Empty(name);
	}
}
