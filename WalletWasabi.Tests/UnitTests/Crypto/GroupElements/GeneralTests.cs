using NBitcoin.Secp256k1;
using System.Collections.Generic;
using WalletWasabi.Crypto.Groups;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto.GroupElements;

public class GeneralTests
{
	[Fact]
	public void IsIEquitable()
	{
		Assert.True(GroupElement.Infinity is IEquatable<GroupElement>);
	}

	[Fact]
	public void ConstructorThrows()
	{
		var one = new FE(1);
		var two = new FE(2);
		var large = new FE(uint.MaxValue);
		var largest = new FE(uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue);

		Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GE(FE.Zero, FE.Zero)));
		Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GE(one, FE.Zero)));
		Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GE(one, one)));
		Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GE(one, two)));
		Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GE(two, two)));
		Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GE(one, large)));
		Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GE(large, large)));
		Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GE(one, largest)));
		Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GE(largest, largest)));
	}

	[Fact]
	public void ConstructorDoesntThrow()
	{
		Assert.NotNull(new GroupElement(GE.Infinity));
		Assert.NotNull(new GroupElement(EC.G));
	}

	[Fact]
	public void InfinityWorks()
	{
		var a = GroupElement.Infinity;
		var b = new GroupElement(GE.Infinity);
		var c = new GroupElement(new GE(FE.Zero, FE.Zero, infinity: true));
		var d = new GroupElement(new GE(new FE(1), new FE(1), infinity: true));
		var e = new GroupElement(new GE(EC.G.x, EC.G.y, infinity: true));

		Assert.True(a.IsInfinity);
		Assert.True(b.IsInfinity);
		Assert.True(c.IsInfinity);
		Assert.True(d.IsInfinity);
		Assert.True(e.IsInfinity);

		Assert.Equal(a, b);
		Assert.Equal(a, c);
		Assert.Equal(a, d);
		Assert.Equal(a, e);

		Assert.Equal(a.GetHashCode(), b.GetHashCode());
		Assert.Equal(a.GetHashCode(), c.GetHashCode());
		Assert.Equal(a.GetHashCode(), d.GetHashCode());
		Assert.Equal(a.GetHashCode(), e.GetHashCode());

		var singleSet = new HashSet<GroupElement> { a, b, c, d, e };
		Assert.Single(singleSet);
	}

	[Fact]
	public void OneEqualsOne()
	{
		var one = new Scalar(1);
		var a = new GroupElement(EC.G) * one;
		var b = new GroupElement(EC.G) * one;
		Assert.Equal(a, b);
	}

	[Fact]
	public void OneDoesntEqualTwo()
	{
		var one = new Scalar(1);
		var two = new Scalar(2);
		var a = new GroupElement(EC.G) * one;
		var b = new GroupElement(EC.G) * two;
		Assert.NotEqual(a, b);
	}

	[Fact]
	public void NullEquality()
	{
		var one = new Scalar(1);
		var ge = new GroupElement(EC.G) * one;

		// Kinda clunky, but otherwise CodeFactor won't be happy.
		GroupElement? n = null;

		Assert.NotEqual(ge, n);
		Assert.True(ge != n);

		Assert.NotEqual(n, ge);
		Assert.True(n != ge);

		Assert.False(ge.Equals(n));
	}

	[Fact]
	public void InfinityDoesntEqualNotInfinity()
	{
		var one = new Scalar(1);
		var a = new GroupElement(EC.G) * one;
		Assert.NotEqual(a, GroupElement.Infinity);
	}

	[Fact]
	public void TransformationsDontRuinEquality()
	{
		var one = new Scalar(1);
		var gej = EC.G * one;
		var ge = gej.ToGroupElement();

		var a = new GroupElement(ge);
		var b = new GroupElement(EC.G) * one;

		Assert.Equal(a, b);
	}

	[Fact]
	public void ToStringIsNice()
	{
		var expectedGenerator = "Standard Generator, secp256k1_fe x = { 0x02F81798UL, 0x00A056C5UL, 0x028D959FUL, 0x036CB738UL, 0x03029BFCUL, 0x03A1C2C1UL, 0x0206295CUL, 0x02EEB156UL, 0x027EF9DCUL, 0x001E6F99UL, 1, 1 };secp256k1_fe y = { 0x0310D4B8UL, 0x01F423FEUL, 0x014199C4UL, 0x01229A15UL, 0x00FD17B4UL, 0x0384422AUL, 0x024FBFC0UL, 0x03119576UL, 0x027726A3UL, 0x00120EB6UL, 1, 1 };";
		var expectedInfinity = "Infinity";
		var expectedTwo = "secp256k1_fe x = { 0x00709EE5UL, 0x03026E57UL, 0x03CA7ABAUL, 0x012E33BCUL, 0x005C778EUL, 0x01701F36UL, 0x005406E9UL, 0x01F5B4C1UL, 0x039441EDUL, 0x0031811FUL, 1, 1 };secp256k1_fe y = { 0x00CFE52AUL, 0x010C6A54UL, 0x010E1236UL, 0x0194C99BUL, 0x02F7F632UL, 0x019B3ABBUL, 0x00584194UL, 0x030CE68FUL, 0x00FEA63DUL, 0x0006B85AUL, 1, 1 };";

		Assert.Equal(expectedGenerator, Generators.G.ToString());
		Assert.Equal(expectedInfinity, GroupElement.Infinity.ToString());
		Assert.Equal(expectedTwo, (new GroupElement(EC.G) * new Scalar(2)).ToString());

		var expectedOther = $"{nameof(Generators.Gw)} Generator, secp256k1_fe x = {{ 0x039DDC14UL, 0x020074B1UL, 0x00B3D361UL, 0x027B3B02UL, 0x02C12FE3UL, 0x004D2976UL, 0x00CF2867UL, 0x0282C917UL, 0x01B623A8UL, 0x002D37D2UL, 1, 1 }};secp256k1_fe y = {{ 0x02503CC2UL, 0x00E8B971UL, 0x0292D707UL, 0x00A80377UL, 0x010F1698UL, 0x017B2B88UL, 0x020E37DCUL, 0x0092BF3FUL, 0x00D655C0UL, 0x0015FE20UL, 1, 1 }};";
		Assert.Equal(expectedOther, Generators.Gw.ToString());
		expectedOther = $"{nameof(Generators.GV)} Generator, secp256k1_fe x = {{ 0x03204C2CUL, 0x03D4FCEBUL, 0x02146437UL, 0x00ED31BCUL, 0x0197F4AAUL, 0x02F42839UL, 0x013E315FUL, 0x03B685BBUL, 0x038468DCUL, 0x001997A6UL, 1, 1 }};secp256k1_fe y = {{ 0x031F8D35UL, 0x035D5330UL, 0x0197E4E2UL, 0x016F5007UL, 0x02FB31FBUL, 0x00C3E125UL, 0x02950EB0UL, 0x00EBB3ACUL, 0x032344BAUL, 0x0006BB8FUL, 1, 1 }};";
		Assert.Equal(expectedOther, Generators.GV.ToString());
		expectedOther = $"{nameof(Generators.Ga)} Generator, secp256k1_fe x = {{ 0x0196E0ECUL, 0x0340A8BAUL, 0x0063C751UL, 0x023F88A7UL, 0x027B1D1FUL, 0x029C6BC9UL, 0x021328A5UL, 0x0283F209UL, 0x02084B4FUL, 0x002AE3D1UL, 1, 1 }};secp256k1_fe y = {{ 0x031D2311UL, 0x00CCF1D5UL, 0x011CA37EUL, 0x029873DBUL, 0x021E5418UL, 0x01C42C25UL, 0x01CABF4DUL, 0x02D8FE75UL, 0x00BC5614UL, 0x000C92BEUL, 1, 1 }};";
		Assert.Equal(expectedOther, Generators.Ga.ToString());
		expectedOther = $"{nameof(Generators.Gg)} Generator, secp256k1_fe x = {{ 0x020071A9UL, 0x0071090AUL, 0x03474E6CUL, 0x0055A460UL, 0x026269E0UL, 0x029AE24EUL, 0x00BAA1CFUL, 0x02F5A259UL, 0x00ACD9CBUL, 0x003EE21AUL, 1, 1 }};secp256k1_fe y = {{ 0x003EE688UL, 0x037AFA80UL, 0x01B81D6FUL, 0x00461688UL, 0x015ED9F9UL, 0x00EE9198UL, 0x01100EA0UL, 0x01A71200UL, 0x03242316UL, 0x000296BDUL, 1, 1 }};";
		Assert.Equal(expectedOther, Generators.Gg.ToString());
		expectedOther = $"{nameof(Generators.Gh)} Generator, secp256k1_fe x = {{ 0x039DFF80UL, 0x01B8481BUL, 0x011ABBE9UL, 0x00BE8310UL, 0x00E65A53UL, 0x01BF0AE1UL, 0x02D77788UL, 0x0305D9C7UL, 0x010CE7A8UL, 0x000F4478UL, 1, 1 }};secp256k1_fe y = {{ 0x039CB37EUL, 0x031CFCCDUL, 0x008B5F30UL, 0x034A60F4UL, 0x02B7411EUL, 0x02C56245UL, 0x03E523ABUL, 0x02731055UL, 0x01ED3B65UL, 0x0036402AUL, 1, 1 }};";
		Assert.Equal(expectedOther, Generators.Gh.ToString());
		expectedOther = $"{nameof(Generators.Gs)} Generator, secp256k1_fe x = {{ 0x032EB0C8UL, 0x031FDE97UL, 0x01CEE3B2UL, 0x03C430CCUL, 0x027408AFUL, 0x03F9A77FUL, 0x0366198CUL, 0x027A7A0CUL, 0x01ED62B7UL, 0x00079DDDUL, 1, 1 }};secp256k1_fe y = {{ 0x012380AFUL, 0x01222A5AUL, 0x00D98FCDUL, 0x00B287E9UL, 0x024D8DEAUL, 0x00D3D2FEUL, 0x024050A3UL, 0x00A9C282UL, 0x015D5C68UL, 0x00217048UL, 1, 1 }};";
		Assert.Equal(expectedOther, Generators.Gs.ToString());
		expectedOther = $"{nameof(Generators.Gwp)} Generator, secp256k1_fe x = {{ 0x036E3B48UL, 0x02E1AC84UL, 0x02CF2B6FUL, 0x03E9E6DCUL, 0x024AE720UL, 0x035D75C8UL, 0x022E662EUL, 0x017A5DC5UL, 0x01578FCEUL, 0x003D4099UL, 1, 1 }};secp256k1_fe y = {{ 0x0200D99FUL, 0x03D25A5FUL, 0x00AE0647UL, 0x006280D7UL, 0x028BB6EBUL, 0x017686F1UL, 0x03EAA210UL, 0x012B5DE8UL, 0x00FF05ADUL, 0x002C81D6UL, 1, 1 }};";
		Assert.Equal(expectedOther, Generators.Gwp.ToString());
		expectedOther = $"{nameof(Generators.Gx0)} Generator, secp256k1_fe x = {{ 0x02763B54UL, 0x03FF007CUL, 0x0186AAA4UL, 0x01506417UL, 0x03499928UL, 0x0054F6DCUL, 0x003ECB12UL, 0x02228B4FUL, 0x033CBE63UL, 0x0038CF27UL, 1, 1 }};secp256k1_fe y = {{ 0x019866EEUL, 0x00A7BD97UL, 0x01AAABF7UL, 0x035DB724UL, 0x00BDB08AUL, 0x016DE340UL, 0x0054B6B6UL, 0x03E7D907UL, 0x03EE4879UL, 0x0001EC61UL, 1, 1 }};";
		Assert.Equal(expectedOther, Generators.Gx0.ToString());
		expectedOther = $"{nameof(Generators.Gx1)} Generator, secp256k1_fe x = {{ 0x028121CCUL, 0x00C4B016UL, 0x029F84DEUL, 0x000500E3UL, 0x03455CA2UL, 0x02953B7BUL, 0x02278AB9UL, 0x02DE26EAUL, 0x00C926AAUL, 0x0011894FUL, 1, 1 }};secp256k1_fe y = {{ 0x0334754AUL, 0x03995CA1UL, 0x016711E7UL, 0x002CD54FUL, 0x002D9E85UL, 0x03BA6CA6UL, 0x00BA4EDBUL, 0x00249F55UL, 0x01345D8BUL, 0x00052B39UL, 1, 1 }};";
		Assert.Equal(expectedOther, Generators.Gx1.ToString());
	}

	private byte[] FillByteArray(int length, byte character)
	{
		var array = new byte[length];
		Array.Fill(array, character);
		return array;
	}

	[Fact]
	public void DeserializationThrows()
	{
		Assert.ThrowsAny<ArgumentException>(() => GroupElement.FromBytes(Array.Empty<byte>()));
		Assert.ThrowsAny<ArgumentException>(() => GroupElement.FromBytes(new byte[] { 0 }));
		Assert.ThrowsAny<ArgumentException>(() => GroupElement.FromBytes(new byte[] { 1 }));
		Assert.ThrowsAny<ArgumentException>(() => GroupElement.FromBytes(new byte[] { 0, 1 }));
		Assert.ThrowsAny<ArgumentException>(() => GroupElement.FromBytes(new byte[] { 0, 1 }));

		Assert.ThrowsAny<ArgumentException>(() => GroupElement.FromBytes(FillByteArray(length: 32, character: 0)));
		Assert.ThrowsAny<ArgumentException>(() => GroupElement.FromBytes(FillByteArray(length: 63, character: 0)));
		var infinity = GroupElement.FromBytes(FillByteArray(length: 33, character: 0));
		Assert.True(infinity.IsInfinity);
		Assert.ThrowsAny<ArgumentException>(() => GroupElement.FromBytes(FillByteArray(length: 64, character: 1)));
		Assert.ThrowsAny<ArgumentException>(() => GroupElement.FromBytes(FillByteArray(length: 64, character: 2)));
		Assert.ThrowsAny<ArgumentException>(() => GroupElement.FromBytes(FillByteArray(length: 65, character: 0)));

		Assert.ThrowsAny<ArgumentException>(() => GroupElement.FromBytes(ByteHelpers.FromHex("100000000000000000000000000000000000000000000000000000000000000000")));
	}

	[Theory]
	[InlineData("000000000000000000000000000000000000000000000000000000000000000000")]
	[InlineData("001000000000000000000000000000000000000000000000000000000000000000")]
	[InlineData("002000000000000000000000000000000000000000000000000000000000000000")]
	[InlineData("003000000000000000000000000000000000000000000000000000000000000000")]
	[InlineData("000000000000000000000000000000000000000000000000000000000000000001")]
	public void InfinityDeserialization(string infinityHex)
	{
		var infinity = GroupElement.FromBytes(ByteHelpers.FromHex(infinityHex));
		Assert.True(infinity.IsInfinity);
		Assert.Equal(GroupElement.Infinity, infinity);
	}

	[Fact]
	public void Serialization()
	{
		var ge = Generators.G;
		var ge2 = GroupElement.FromBytes(ge.ToBytes());
		Assert.Equal(ge, ge2);

		ge = GroupElement.Infinity;
		ge2 = GroupElement.FromBytes(ge.ToBytes());
		Assert.Equal(ge, ge2);

		// Make sure infinity definition isn't f*d up in the constructor as the frombytes relies on special zero coordinates for infinity.
		// 1. Try defining infinity with non-zero coordinates should work:
		ge = new GroupElement(new GE(EC.G.x, EC.G.y, infinity: true));
		byte[] zeroBytes = ge.ToBytes();
		ge2 = GroupElement.FromBytes(zeroBytes);
		Assert.Equal(GroupElement.Infinity, ge2);

		// 2. Try defining non-infinity with zero coordinates should not work.
		Assert.ThrowsAny<ArgumentException>(() => new GroupElement(new GE(FE.Zero, FE.Zero, infinity: false)));

		ge = new GroupElement(EC.G) * new Scalar(1);
		ge2 = GroupElement.FromBytes(ge.ToBytes());
		Assert.Equal(ge, ge2);

		ge = new GroupElement(EC.G) * new Scalar(2);
		ge2 = GroupElement.FromBytes(ge.ToBytes());
		Assert.Equal(ge, ge2);

		ge = new GroupElement(EC.G) * new Scalar(3);
		ge2 = GroupElement.FromBytes(ge.ToBytes());
		Assert.Equal(ge, ge2);

		ge = new GroupElement(EC.G) * new Scalar(21);
		ge2 = GroupElement.FromBytes(ge.ToBytes());
		Assert.Equal(ge, ge2);

		ge = new GroupElement(EC.G) * EC.NC;
		ge2 = GroupElement.FromBytes(ge.ToBytes());
		Assert.Equal(ge, ge2);

		ge = new GroupElement(EC.G) * EC.N;
		ge2 = GroupElement.FromBytes(ge.ToBytes());
		Assert.Equal(ge, ge2);
	}

	[Fact]
	public void EvenOddSerialization()
	{
		var hexG = "0279BE667EF9DCBBAC55A06295CE870B07029BFCDB2DCE28D959F2815B16F81798";
		var hexGodd = "0379BE667EF9DCBBAC55A06295CE870B07029BFCDB2DCE28D959F2815B16F81798";

		Assert.Equal(hexG, ByteHelpers.ToHex(Generators.G.ToBytes()));
		Assert.Equal(Generators.G, GroupElement.FromBytes(ByteHelpers.FromHex(hexG)));

		Assert.Equal(hexGodd, ByteHelpers.ToHex(Generators.G.Negate().ToBytes()));
		Assert.Equal(Generators.G.Negate(), GroupElement.FromBytes(ByteHelpers.FromHex(hexGodd)));
	}

	[Fact]
	public void PointOutOfCurveDeserialization()
	{
		var serialized = FillByteArray(length: 33, character: 0);
		serialized[0] = GE.SECP256K1_TAG_PUBKEY_EVEN;
		Assert.ThrowsAny<ArgumentException>(() => GroupElement.FromBytes(serialized));

		serialized[0] = GE.SECP256K1_TAG_PUBKEY_ODD;
		Assert.ThrowsAny<ArgumentException>(() => GroupElement.FromBytes(serialized));
	}

	[Fact]
	public void NonNormalizedPointDeserialization()
	{
		var p = FE.CONST(
			0xFFFFFC2FU,
			0xFFFFFFFFU,
			0xFFFFFFFFU,
			0xFFFFFFFFU,
			0xFFFFFFFFU,
			0xFFFFFFFFU,
			0xFFFFFFFFU,
			0xFFFFFFFFU);
		var x = EC.G.x;
		x = x.Add(p);
		var x3 = x * x * x;
		var y2 = x3 + new FE(EC.CURVE_B);
		Assert.True(y2.Sqrt(out var y));
		var ge = new GroupElement(new GE(x, y));

		var ge2 = GroupElement.FromBytes(ge.ToBytes());
		Assert.Equal(ge, ge2);
	}

	[Theory]
	[InlineData("", "02E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855")]
	[InlineData(" ", "0224944F33566D9ED9C410AE72F89454AC6F0CFEE446590C01751F094E185E8978")]
	[InlineData("  ", "026C179F21E6F62B629055D8AB40F454ED02E48B68563913473B857D3638E23B28")]
	[InlineData("a", "02EB48BDFA15FC43DBEA3AABB1EE847B6E69232C0F0D9705935E50D60CCE77877F")]
	[InlineData("12345", "035994471ABB01112AFCC18159F6CC74B4F511B99806DA59B3CAF5A9C173CACFC5")]
	public void FromText(string text, string expectedHex)
	{
		var ge = Generators.FromText(text);
		var hex = ByteHelpers.ToHex(ge.ToBytes());
		Assert.Equal(expectedHex, hex);
	}

	[Fact]
	public void ToHex()
	{
		Assert.Throws<NullReferenceException>(() => ByteHelpers.ToHex(null!));
		Assert.Equal("", ByteHelpers.ToHex(Array.Empty<byte>()));
		Assert.Equal("0102", ByteHelpers.ToHex(0x01, 0x02));
	}
}
