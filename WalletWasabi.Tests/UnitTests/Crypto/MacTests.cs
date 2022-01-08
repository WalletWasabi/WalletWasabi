using NBitcoin.Secp256k1;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto;

public class MacTests
{
	[Fact]
	[Trait("UnitTest", "UnitTest")]
	public void CannotBuildWrongMac()
	{
		using var rnd = new SecureRandom();
		var sk = new CredentialIssuerSecretKey(rnd);

		Assert.Throws<ArgumentNullException>(() => MAC.ComputeMAC(null!, Generators.G, Scalar.One));
		Assert.Throws<ArgumentNullException>(() => MAC.ComputeMAC(sk, null!, Scalar.One));
		var ex = Assert.Throws<ArgumentException>(() => MAC.ComputeMAC(sk, Generators.G, Scalar.Zero));
		Assert.StartsWith("Value cannot be zero.", ex.Message);
	}

	[Fact]
	[Trait("UnitTest", "UnitTest")]
	public void CanProduceAndVerifyMAC()
	{
		using var rnd = new SecureRandom();
		var sk = new CredentialIssuerSecretKey(rnd);

		var attribute = rnd.GetScalar() * Generators.G;  // any random point
		var t = rnd.GetScalar();

		var mac = MAC.ComputeMAC(sk, attribute, t);

		Assert.True(mac.VerifyMAC(sk, attribute));
	}

	[Fact]
	[Trait("UnitTest", "UnitTest")]
	public void CanDetectInvalidMAC()
	{
		using var rnd = new SecureRandom();
		var sk = new CredentialIssuerSecretKey(rnd);

		var attribute = rnd.GetScalar() * Generators.G;  // any random point
		var differentAttribute = rnd.GetScalar() * Generators.G;  // any other random point
		var t = rnd.GetScalar();

		// Create MAC for realAttribute and verify with fake/wrong attribute
		var mac = MAC.ComputeMAC(sk, attribute, t);
		Assert.False(mac.VerifyMAC(sk, differentAttribute));

		var differentT = rnd.GetScalar();
		var differentMac = MAC.ComputeMAC(sk, attribute, differentT);
		Assert.NotEqual(mac, differentMac);

		mac = MAC.ComputeMAC(sk, attribute, differentT);
		var differentSk = new CredentialIssuerSecretKey(rnd);
		Assert.False(mac.VerifyMAC(differentSk, attribute));
	}

	[Fact]
	[Trait("UnitTest", "UnitTest")]
	public void EqualityTests()
	{
		using var rnd = new SecureRandom();

		var right = (attribute: rnd.GetScalar() * Generators.G, sk: new CredentialIssuerSecretKey(rnd), t: rnd.GetScalar());
		var wrong = (attribute: rnd.GetScalar() * Generators.G, sk: new CredentialIssuerSecretKey(rnd), t: rnd.GetScalar());

		var cases = new[]
		{
				(right.attribute, right.sk, right.t, isEqual: true),
				(right.attribute, right.sk, wrong.t, isEqual: false),
				(right.attribute, wrong.sk, right.t, isEqual: false),
				(right.attribute, wrong.sk, wrong.t, isEqual: false),
				(wrong.attribute, right.sk, right.t, isEqual: false),
				(wrong.attribute, right.sk, wrong.t, isEqual: false),
				(wrong.attribute, wrong.sk, right.t, isEqual: false),
				(wrong.attribute, wrong.sk, wrong.t, isEqual: false),
			};

		var mac = MAC.ComputeMAC(right.sk, right.attribute, right.t);

		foreach (var c in cases)
		{
			var cmac = MAC.ComputeMAC(c.sk, c.attribute, c.t);
			Assert.Equal(c.isEqual, mac == cmac);
			Assert.Equal(c.isEqual, cmac == mac);
		}

		Assert.True(mac.Equals(mac));

		MAC? nullMac = null;
		Assert.False(mac.Equals(nullMac));
	}
}
