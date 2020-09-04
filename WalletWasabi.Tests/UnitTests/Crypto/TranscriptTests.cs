using System.Text;
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

			var secretNonceProvider1 = transcript1.CreateSyntheticSecretNonceProvider(witness1, rnd);
			var secretNonceProvider2 = transcript2.CreateSyntheticSecretNonceProvider(witness1, rnd);
			var secretNonceProvider3 = transcript3.CreateSyntheticSecretNonceProvider(witness2, rnd);
			var secretNonceProvider4 = transcript4.CreateSyntheticSecretNonceProvider(witness2, rnd);

			var secretNonce1 = secretNonceProvider1.GetScalar();
			var secretNonce2 = secretNonceProvider2.GetScalar();
			var secretNonce3 = secretNonceProvider3.GetScalar();
			var secretNonce4 = secretNonceProvider4.GetScalar();

			Assert.NotEqual(secretNonce1, secretNonce2);
			Assert.NotEqual(secretNonce1, secretNonce3);
			Assert.NotEqual(secretNonce1, secretNonce4);

			Assert.NotEqual(secretNonce2, secretNonce3);
			Assert.NotEqual(secretNonce2, secretNonce4);

			Assert.NotEqual(secretNonce3, secretNonce4);
		}
	}
}
