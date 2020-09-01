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

			var nonceGenerator1 = transcript1.CreateSyntheticNocesProvider(witness1, rnd);
			var nonceGenerator2 = transcript2.CreateSyntheticNocesProvider(witness1, rnd);
			var nonceGenerator3 = transcript3.CreateSyntheticNocesProvider(witness2, rnd);
			var nonceGenerator4 = transcript4.CreateSyntheticNocesProvider(witness2, rnd);

			var nonce1 = nonceGenerator1().First();
			var nonce2 = nonceGenerator2().First();
			var nonce3 = nonceGenerator3().First();
			var nonce4 = nonceGenerator4().First();

			Assert.NotEqual(nonce1, nonce2);
			Assert.NotEqual(nonce1, nonce3);
			Assert.NotEqual(nonce1, nonce4);

			Assert.NotEqual(nonce2, nonce3);
			Assert.NotEqual(nonce2, nonce4);

			Assert.NotEqual(nonce3, nonce4);
		}
	}
}