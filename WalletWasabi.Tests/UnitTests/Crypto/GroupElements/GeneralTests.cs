using NBitcoin.Secp256k1;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WalletWasabi.Crypto.Groups;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto.GroupElements
{
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

			Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GEJ(FE.Zero, FE.Zero, FE.Zero)));
			Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GEJ(one, FE.Zero, FE.Zero)));
			Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GEJ(one, one, one)));
			Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GEJ(one, one, two)));
			Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GEJ(two, two, two)));
			Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GEJ(one, one, large)));
			Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GEJ(large, large, large)));
			Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GEJ(one, one, largest)));
			Assert.Throws<ArgumentOutOfRangeException>(() => new GroupElement(new GEJ(largest, largest, largest)));
		}

		[Fact]
		public void ConstructorDoesntThrow()
		{
			new GroupElement(GE.Infinity);
			new GroupElement(GEJ.Infinity);
			new GroupElement(EC.G);
			new GroupElement(EC.G * new Scalar(1));
			new GroupElement(EC.G * new Scalar(uint.MaxValue));
			new GroupElement(EC.G * new Scalar(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue));
		}

		[Fact]
		public void InfinityWorks()
		{
			var a = GroupElement.Infinity;
			var b = new GroupElement(GE.Infinity);
			var c = new GroupElement(new GE(FE.Zero, FE.Zero, infinity: true));
			var d = new GroupElement(new GE(new FE(1), new FE(1), infinity: true));
			var e = new GroupElement(GEJ.Infinity);
			var f = new GroupElement(new GEJ(FE.Zero, FE.Zero, FE.Zero, infinity: true));
			var g = new GroupElement(new GEJ(new FE(1), new FE(1), new FE(1), infinity: true));
			var h = new GroupElement(new GE(EC.G.x, EC.G.y, infinity: true));
			var i = new GroupElement(EC.G * Scalar.Zero);

			Assert.True(a.IsInfinity);
			Assert.True(b.IsInfinity);
			Assert.True(c.IsInfinity);
			Assert.True(d.IsInfinity);
			Assert.True(e.IsInfinity);
			Assert.True(f.IsInfinity);
			Assert.True(g.IsInfinity);
			Assert.True(h.IsInfinity);
			Assert.True(i.IsInfinity);

			Assert.Equal(a, b);
			Assert.Equal(a, c);
			Assert.Equal(a, d);
			Assert.Equal(a, e);
			Assert.Equal(a, f);
			Assert.Equal(a, g);
			Assert.Equal(a, h);
			Assert.Equal(a, i);

			Assert.Equal(a.GetHashCode(), b.GetHashCode());
			Assert.Equal(a.GetHashCode(), c.GetHashCode());
			Assert.Equal(a.GetHashCode(), d.GetHashCode());
			Assert.Equal(a.GetHashCode(), e.GetHashCode());
			Assert.Equal(a.GetHashCode(), f.GetHashCode());
			Assert.Equal(a.GetHashCode(), g.GetHashCode());
			Assert.Equal(a.GetHashCode(), h.GetHashCode());
			Assert.Equal(a.GetHashCode(), i.GetHashCode());

			var singleSet = new HashSet<GroupElement> { a, b, c, d, e, f, g, h, i };
			Assert.Single(singleSet);
		}

		[Fact]
		public void OneEqualsOne()
		{
			var one = new Scalar(1);
			var a = new GroupElement(EC.G * one);
			var b = new GroupElement(EC.G * one);
			Assert.Equal(a, b);
		}

		[Fact]
		public void OneDoesntEqualTwo()
		{
			var one = new Scalar(1);
			var two = new Scalar(2);
			var a = new GroupElement(EC.G * one);
			var b = new GroupElement(EC.G * two);
			Assert.NotEqual(a, b);
		}

		[Fact]
		public void NullEquality()
		{
			var one = new Scalar(1);
			var ge = new GroupElement(EC.G * one);

			// Kinda clunky, but otherwise CodeFactor won't be happy.
			GroupElement? n = null;

			Assert.False(ge == n);
			Assert.True(ge != n);

			Assert.False(n == ge);
			Assert.True(n != ge);

			Assert.False(ge.Equals(n));
		}

		[Fact]
		public void InfinityDoesntEqualNotInfinity()
		{
			var one = new Scalar(1);
			var a = new GroupElement(EC.G * one);
			Assert.NotEqual(a, GroupElement.Infinity);
		}

		[Fact]
		public void TransformationsDontRuinEquality()
		{
			var one = new Scalar(1);
			var gej = EC.G * one;
			var ge = gej.ToGroupElement();
			var sameGej = ge.ToGroupElementJacobian();

			var a = new GroupElement(ge);
			var b = new GroupElement(gej);
			var c = new GroupElement(sameGej);

			Assert.Equal(a, b);
			Assert.Equal(a, c);
		}

		[Fact]
		public void ToStringIsNice()
		{
			var expectedGenerator = "Standard Generator, secp256k1_fe x = { 0x02F81798UL, 0x00A056C5UL, 0x028D959FUL, 0x036CB738UL, 0x03029BFCUL, 0x03A1C2C1UL, 0x0206295CUL, 0x02EEB156UL, 0x027EF9DCUL, 0x001E6F99UL, 1, 1 };secp256k1_fe y = { 0x0310D4B8UL, 0x01F423FEUL, 0x014199C4UL, 0x01229A15UL, 0x00FD17B4UL, 0x0384422AUL, 0x024FBFC0UL, 0x03119576UL, 0x027726A3UL, 0x00120EB6UL, 1, 1 };";
			var expectedInfinity = "Infinity";
			var expectedTwo = "secp256k1_fe x = { 0x00709EE5UL, 0x03026E57UL, 0x03CA7ABAUL, 0x012E33BCUL, 0x005C778EUL, 0x01701F36UL, 0x005406E9UL, 0x01F5B4C1UL, 0x039441EDUL, 0x0031811FUL, 1, 1 };secp256k1_fe y = { 0x00CFE52AUL, 0x010C6A54UL, 0x010E1236UL, 0x0194C99BUL, 0x02F7F632UL, 0x019B3ABBUL, 0x00584194UL, 0x030CE68FUL, 0x00FEA63DUL, 0x0006B85AUL, 1, 1 };";

			Assert.Equal(expectedGenerator, Generators.G.ToString());
			Assert.Equal(expectedInfinity, GroupElement.Infinity.ToString());
			Assert.Equal(expectedTwo, new GroupElement(EC.G * new Scalar(2)).ToString());

			var expectedOther = $"{nameof(Generators.Gw)} Generator, secp256k1_fe x = {{ 0x01013C2FUL, 0x02740389UL, 0x008F54EFUL, 0x0113E5FFUL, 0x01427B04UL, 0x013DC46AUL, 0x024707DBUL, 0x026081FEUL, 0x02E54CCBUL, 0x000E292AUL, 1, 1 }};secp256k1_fe y = {{ 0x011FAF6BUL, 0x00B7E446UL, 0x00391D3CUL, 0x02DC9E83UL, 0x0104B9F8UL, 0x00A538B6UL, 0x0300C87BUL, 0x03B57940UL, 0x0176D2D7UL, 0x00219279UL, 1, 1 }};";
			Assert.Equal(expectedOther, Generators.Gw.ToString());
			expectedOther = $"{nameof(Generators.GV)} Generator, secp256k1_fe x = {{ 0x01C14967UL, 0x032B8C8CUL, 0x00BCF412UL, 0x02990EB3UL, 0x036E253AUL, 0x010A2C08UL, 0x03184692UL, 0x010747EDUL, 0x000F8A3BUL, 0x0008B07BUL, 1, 1 }};secp256k1_fe y = {{ 0x03CE6291UL, 0x016336FEUL, 0x02C3A482UL, 0x03FE0AD3UL, 0x00E48CBCUL, 0x03359AC5UL, 0x027BD948UL, 0x02022579UL, 0x01C5FA64UL, 0x00172057UL, 1, 1 }};";
			Assert.Equal(expectedOther, Generators.GV.ToString());
			expectedOther = $"{nameof(Generators.Ga)} Generator, secp256k1_fe x = {{ 0x0266ABAEUL, 0x0004B9CFUL, 0x02E7378EUL, 0x00EBC73BUL, 0x018E34BDUL, 0x01312796UL, 0x0234D9B7UL, 0x018E7FE4UL, 0x018F3EB9UL, 0x000BDEFCUL, 1, 1 }};secp256k1_fe y = {{ 0x0006FA9BUL, 0x01CE10D2UL, 0x03470431UL, 0x006E8AF0UL, 0x010326A6UL, 0x0175DCEDUL, 0x019B66BFUL, 0x00F74680UL, 0x03D9DD5FUL, 0x002BCC33UL, 1, 1 }};";
			Assert.Equal(expectedOther, Generators.Ga.ToString());
			expectedOther = $"{nameof(Generators.Gg)} Generator, secp256k1_fe x = {{ 0x026C277DUL, 0x00C330E9UL, 0x00EAC3DEUL, 0x0359F156UL, 0x03047FE9UL, 0x00C6F7BAUL, 0x03130E6AUL, 0x01535284UL, 0x00A880CBUL, 0x0011E1F1UL, 1, 1 }};secp256k1_fe y = {{ 0x018C0772UL, 0x00789C4DUL, 0x00C7FBD0UL, 0x02F58835UL, 0x02321AA5UL, 0x0331FF52UL, 0x03063B93UL, 0x02EAD3B9UL, 0x00CD2B17UL, 0x003A8A67UL, 1, 1 }};";
			Assert.Equal(expectedOther, Generators.Gg.ToString());
			expectedOther = $"{nameof(Generators.Gh)} Generator, secp256k1_fe x = {{ 0x03DEAC03UL, 0x02849530UL, 0x006C9A8DUL, 0x03A97B7BUL, 0x02C1B389UL, 0x01F35E2FUL, 0x017513A7UL, 0x01A34B45UL, 0x033B63E0UL, 0x0016BC8EUL, 1, 1 }};secp256k1_fe y = {{ 0x0145B539UL, 0x032AA24FUL, 0x0169F63DUL, 0x02F69B76UL, 0x0267A0ADUL, 0x01D42286UL, 0x00A32FC8UL, 0x01165B84UL, 0x0057FC53UL, 0x001364BFUL, 1, 1 }};";
			Assert.Equal(expectedOther, Generators.Gh.ToString());
			expectedOther = $"{nameof(Generators.Gs)} Generator, secp256k1_fe x = {{ 0x008C7263UL, 0x03D6A57FUL, 0x0367F61CUL, 0x017F373BUL, 0x038D86E3UL, 0x03BF070AUL, 0x02E2F848UL, 0x01997C26UL, 0x0090B66BUL, 0x0006DE19UL, 1, 1 }};secp256k1_fe y = {{ 0x038CCC8BUL, 0x028AAAFEUL, 0x01B21F36UL, 0x0003B3E9UL, 0x01BA251EUL, 0x03B46C48UL, 0x02C48003UL, 0x019FD6C4UL, 0x016C02D3UL, 0x00202E60UL, 1, 1 }};";
			Assert.Equal(expectedOther, Generators.Gs.ToString());
			expectedOther = $"{nameof(Generators.Gwp)} Generator, secp256k1_fe x = {{ 0x03A4F16CUL, 0x01610768UL, 0x017DAC47UL, 0x036A2588UL, 0x019961D9UL, 0x038F8689UL, 0x01B0D71FUL, 0x03266FC3UL, 0x021F53BFUL, 0x00291589UL, 1, 1 }};secp256k1_fe y = {{ 0x0069FEE2UL, 0x03EF9C5DUL, 0x00990987UL, 0x0123332DUL, 0x02BF866AUL, 0x00A2CB4CUL, 0x01DE709CUL, 0x02036AA6UL, 0x01D2B679UL, 0x001F716FUL, 1, 1 }};";
			Assert.Equal(expectedOther, Generators.Gwp.ToString());
			expectedOther = $"{nameof(Generators.Gx0)} Generator, secp256k1_fe x = {{ 0x00282D76UL, 0x00B3C8A0UL, 0x002FCDC6UL, 0x01C2A54BUL, 0x01CCE6D4UL, 0x01531403UL, 0x03250329UL, 0x0098CA40UL, 0x00F60042UL, 0x002079EFUL, 1, 1 }};secp256k1_fe y = {{ 0x02134910UL, 0x015FFE0DUL, 0x028F258DUL, 0x02473F56UL, 0x00B9B563UL, 0x01324EA8UL, 0x037EA618UL, 0x024B4CB2UL, 0x01A9AB0FUL, 0x00179972UL, 1, 1 }};";
			Assert.Equal(expectedOther, Generators.Gx0.ToString());
			expectedOther = $"{nameof(Generators.Gx1)} Generator, secp256k1_fe x = {{ 0x001A5105UL, 0x029F1DCBUL, 0x02C35E8DUL, 0x01D06A6CUL, 0x006748FAUL, 0x0120993CUL, 0x00241D5EUL, 0x000B8FA1UL, 0x02979549UL, 0x00280C7FUL, 1, 1 }};secp256k1_fe y = {{ 0x02601941UL, 0x017DC826UL, 0x0392A4CCUL, 0x000E1939UL, 0x02BDFDB2UL, 0x000D9228UL, 0x01EBCC89UL, 0x00F3257FUL, 0x01312A11UL, 0x000BFA1CUL, 1, 1 }};";
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

			ge = new GroupElement(EC.G * new Scalar(1));
			ge2 = GroupElement.FromBytes(ge.ToBytes());
			Assert.Equal(ge, ge2);

			ge = new GroupElement(EC.G * new Scalar(2));
			ge2 = GroupElement.FromBytes(ge.ToBytes());
			Assert.Equal(ge, ge2);

			ge = new GroupElement(EC.G * new Scalar(3));
			ge2 = GroupElement.FromBytes(ge.ToBytes());
			Assert.Equal(ge, ge2);

			ge = new GroupElement(EC.G * new Scalar(21));
			ge2 = GroupElement.FromBytes(ge.ToBytes());
			Assert.Equal(ge, ge2);

			ge = new GroupElement(EC.G * EC.NC);
			ge2 = GroupElement.FromBytes(ge.ToBytes());
			Assert.Equal(ge, ge2);

			ge = new GroupElement(EC.G * EC.N);
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
		[InlineData("", "035FECEB66FFC86F38D952786C6D696C79C2DBC239DD4E91B46729D73A27FB57E9")]
		[InlineData(" ", "03E12A7E051731CF1DBEEFA2142A8E1ABB1EB5898E2CBE4AA522120829A5588DC7")]
		[InlineData("  ", "0297D2E845C60987D38F0A97F9E0E0BC9946BF55A499A1F0E5257B0978BBEC85E3")]
		[InlineData("a", "024E1195DF020DE59E0D65A33A4279F1183E7AE4E5D980E309F8B55ADFF2E61C3E")]
		[InlineData("12345", "02DD712114FB283417DE4DA3512E17486ADBDA004060D0D1646508C8A2740D29B4")]
		public void FromText(string text, string expectedHex)
		{
			var ge = GroupElement.FromText(text);
			var hex = ByteHelpers.ToHex(ge.ToBytes());
			Assert.Equal(expectedHex, hex);
		}
	}
}
