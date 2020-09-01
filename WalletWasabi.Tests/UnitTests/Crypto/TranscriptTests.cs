using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin.Secp256k1;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto
{
	public class TranscriptTests
	{
		[Fact]
		public void SimpleEquivalenceTest()
		{
			var protocol = Encoding.UTF8.GetBytes("test protocol");
			var realTranscript = new Transcript(protocol);
			var testTranscript = new TestTranscript(protocol);

			realTranscript.CommitPublicNonces(new[] { Generators.G });
			testTranscript.CommitPublicNonces(new[] { Generators.G });

			var realChallenge = realTranscript.GenerateChallenge();
			var testChallenge = testTranscript.GenerateChallenge();

			Assert.Equal(realChallenge, testChallenge);
		}

		[Fact]
		public void ComplexEquivalenceTest()
		{
			var protocol = Encoding.UTF8.GetBytes("test protocol");
			var realTranscript = new Transcript(protocol);
			var testTranscript = new TestTranscript(protocol);

			realTranscript.CommitPublicNonces(new[] { Generators.G });
			testTranscript.CommitPublicNonces(new[] { Generators.G });

			for (var i = 0; i < 32; i++)
			{
				var realChallenge = realTranscript.GenerateChallenge();
				var testChallenge = testTranscript.GenerateChallenge();

				Assert.Equal(realChallenge, testChallenge);

				realTranscript.CommitPublicNonces(new[] { Generators.G, Generators.GV });
				testTranscript.CommitPublicNonces(new[] { Generators.G, Generators.GV });
			}
		}

		[Fact]
		public void SyntheticNoncesTest()
		{
			var protocol = Encoding.UTF8.GetBytes("test TranscriptRng collisions");
			var rnd = new SecureRandom();

			var commitment1 = new[] { Generators.Gx0 };
			var commitment2 = new[] { Generators.Gx1 };
			var witness1 = new[] { rnd.GetScalar() };
			var witness2 = new[] { rnd.GetScalar() };

			var transcript1 = new Transcript(protocol);
			var transcript2 = new Transcript(protocol);
			var transcript3 = new Transcript(protocol);
			var transcript4 = new Transcript(protocol);

			transcript1.CommitPublicNonces(commitment1);
			transcript2.CommitPublicNonces(commitment2);
			transcript3.CommitPublicNonces(commitment2);
			transcript4.CommitPublicNonces(commitment2);

			var publicNonceGenerator1 = transcript1.CreateSyntheticPublicNoncesProvider(witness1, rnd);
			var publicNonceGenerator2 = transcript2.CreateSyntheticPublicNoncesProvider(witness1, rnd);
			var publicNonceGenerator3 = transcript3.CreateSyntheticPublicNoncesProvider(witness2, rnd);
			var publicNonceGenerator4 = transcript4.CreateSyntheticPublicNoncesProvider(witness2, rnd);

			var publicNonce1 = publicNonceGenerator1().First();
			var publicNonce2 = publicNonceGenerator2().First();
			var publicNonce3 = publicNonceGenerator3().First();
			var publicNonce4 = publicNonceGenerator4().First();

			Assert.NotEqual(publicNonce1, publicNonce2);
			Assert.NotEqual(publicNonce1, publicNonce3);
			Assert.NotEqual(publicNonce1, publicNonce4);

			Assert.NotEqual(publicNonce2, publicNonce3);
			Assert.NotEqual(publicNonce2, publicNonce4);

			Assert.NotEqual(publicNonce3, publicNonce4);
		}
	}
}
