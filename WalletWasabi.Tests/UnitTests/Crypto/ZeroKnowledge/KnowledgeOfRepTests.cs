using Moq;
using NBitcoin.Secp256k1;
using System;
using System.Linq;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Crypto.ZeroKnowledge.LinearRelation;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto.ZeroKnowledge
{
	public class KnowledgeOfRepTests
	{
		[Theory]
		[InlineData(1, 1)]
		[InlineData(1, 2)]
		[InlineData(3, 5)]
		[InlineData(5, 7)]
		[InlineData(7, 11)]
		[InlineData(short.MaxValue, uint.MaxValue)]
		[InlineData(int.MaxValue, uint.MaxValue)]
		[InlineData(uint.MaxValue, uint.MaxValue)]
		public void End2EndVerificationSimple(uint scalarSeed1, uint scalarSeed2)
		{
			var secrets = new ScalarVector(new Scalar(scalarSeed1), new Scalar(scalarSeed2));
			var generators = new GroupElementVector(Generators.G, Generators.Ga);
			var publicPoint = secrets * generators;

			var statement = new Statement(publicPoint, generators);
			var mockRandom = new Mock<WasabiRandom>(MockBehavior.Strict);
			mockRandom.Setup(rnd => rnd.GetBytes(32)).Returns(new byte[32]);
			var proof = ProofSystemHelpers.Prove(statement, secrets, mockRandom.Object);
			Assert.True(ProofSystemHelpers.Verify(statement, proof));
		}

		[Fact]
		public void End2EndVerification()
		{
			var goodScalars = CryptoHelpers.GetScalars(x => !x.IsOverflow && !x.IsZero);
			foreach (var secret1 in goodScalars)
			{
				foreach (var secret2 in goodScalars.Where(x => x != secret1))
				{
					var secrets = new ScalarVector(secret1, secret2);
					var generators = new GroupElementVector(Generators.G, Generators.Ga);
					var publicPoint = secrets * generators;
					var statement = new Statement(publicPoint, generators);
					using var rand = new SecureRandom();
					var proof = ProofSystemHelpers.Prove(statement, secrets, rand);
					Assert.True(ProofSystemHelpers.Verify(statement, proof));
				}
			}
		}

		[Fact]
		public void KnowledgeThrows()
		{
			var two = new Scalar(2);
			var three = new Scalar(3);

			// Public point must be sum(generator * secret).
			Assert.ThrowsAny<ArgumentException>(() => new Knowledge(new Statement(three * Generators.Ga, Generators.G, Generators.Ga), new ScalarVector(two, three)).AssertSoundness());
			Assert.ThrowsAny<ArgumentException>(() => new Knowledge(new Statement(two * Generators.G, Generators.G, Generators.Ga), new ScalarVector(two, three)).AssertSoundness());
			Assert.ThrowsAny<ArgumentException>(() => new Knowledge(new Statement(two * Generators.G + three * Generators.Ga, Generators.Ga, Generators.G), new ScalarVector(two, three)).AssertSoundness());
			Assert.ThrowsAny<ArgumentException>(() => new Knowledge(new Statement(two * Generators.G + three * Generators.Ga, Generators.G, Generators.Gg), new ScalarVector(two, three)).AssertSoundness());
			Assert.ThrowsAny<ArgumentException>(() => new Knowledge(new Statement(Generators.G + three * Generators.Ga, Generators.G, Generators.Ga), new ScalarVector(two, three)).AssertSoundness());
			Assert.ThrowsAny<ArgumentException>(() => new Knowledge(new Statement(two * Generators.G + Generators.Ga, Generators.G, Generators.Ga), new ScalarVector(two, three)).AssertSoundness());

			// Generators and secrets must be equal.
			Assert.ThrowsAny<ArgumentException>(() => new Knowledge(new Statement(two * Generators.G + three * Generators.Ga, Generators.Gg, Generators.Ga), new ScalarVector(two)));
			Assert.ThrowsAny<ArgumentException>(() => new Knowledge(new Statement(two * Generators.G + three * Generators.Ga), new ScalarVector(two, three)));
			Assert.ThrowsAny<ArgumentException>(() => new Knowledge(new Statement(two * Generators.G + three * Generators.Ga, Generators.Gg), new ScalarVector(two, three)));
			Assert.ThrowsAny<ArgumentException>(() => new Knowledge(new Statement(two * Generators.G + three * Generators.Ga, Generators.G, Generators.Ga, Generators.Gg), new ScalarVector(two, three)));
		}
	}
}
