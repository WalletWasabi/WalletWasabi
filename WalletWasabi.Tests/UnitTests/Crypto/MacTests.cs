using System;
using NBitcoin.Secp256k1;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto
{
	public class MacTests
	{
		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CannotBuildWrongMac()
		{
			var rnd = new SecureRandom();
			var sk = new CoordinatorSecretKey(rnd);

			Assert.Throws<ArgumentNullException>(() => MAC.ComputeMAC(null!, Generators.G, Scalar.One));
			Assert.Throws<ArgumentNullException>(() => MAC.ComputeMAC(sk, null!, Scalar.One));
			var ex = Assert.Throws<ArgumentException>(() => MAC.ComputeMAC(sk, Generators.G, Scalar.Zero));
			Assert.StartsWith("Value cannot be zero.", ex.Message);
		}


		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanProduceAndVerifyMAC()
		{
			var rnd = new SecureRandom();
			var sk = new CoordinatorSecretKey(rnd);

			var attribute = rnd.GetScalar() * Generators.G;  // any random point
			var t = rnd.GetScalar();

			var mac = MAC.ComputeMAC(sk, attribute, t);

			Assert.True(mac.VerifyMAC(sk, attribute));
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanDetectInvalidMAC()
		{
			var rnd = new SecureRandom();
			var sk = new CoordinatorSecretKey(rnd);

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
			var differentSk = new CoordinatorSecretKey(rnd);
			Assert.False(mac.VerifyMAC(differentSk, attribute));
		}
	}
}