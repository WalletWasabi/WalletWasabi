using NBitcoin.Secp256k1;
using System;
using System.Linq;
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

		[Fact]
		public void SyntheticNoncesSecretDependenceTest()
		{
			var protocol = Encoding.UTF8.GetBytes("test TranscriptRng collisions");

			// if all synthetic nonce provider get the same randomness, nonce sequences
			// with different witnesses or commitments should still diverge
			var rnd = new MockRandom();
			rnd.GetBytesResults.Add(new byte[32]);
			rnd.GetBytesResults.Add(new byte[32]);
			rnd.GetBytesResults.Add(new byte[32]);
			rnd.GetBytesResults.Add(new byte[32]);

			var commitment1 = new[] { Generators.Gx0 };
			var commitment2 = new[] { Generators.Gx1 };
			var witness1 = new[] { Scalar.One };
			var witness2 = new[] { Scalar.Zero };

			var transcript1 = new Transcript(protocol);
			var transcript2 = new Transcript(protocol);
			var transcript3 = new Transcript(protocol);
			var transcript4 = new Transcript(protocol);

			transcript1.CommitPublicNonces(commitment1);
			transcript2.CommitPublicNonces(commitment2);
			transcript3.CommitPublicNonces(commitment2);
			transcript4.CommitPublicNonces(commitment2);

			var secretNonceGenerator1 = transcript1.CreateSyntheticSecretNonceProvider(witness1, rnd);
			var secretNonceGenerator2 = transcript2.CreateSyntheticSecretNonceProvider(witness1, rnd);
			var secretNonceGenerator3 = transcript3.CreateSyntheticSecretNonceProvider(witness2, rnd);
			var secretNonceGenerator4 = transcript4.CreateSyntheticSecretNonceProvider(witness2, rnd);

			Assert.Empty(rnd.GetBytesResults);

			var secretNonce1 = secretNonceGenerator1.GetScalar();
			var secretNonce2 = secretNonceGenerator2.GetScalar();
			var secretNonce3 = secretNonceGenerator3.GetScalar();
			var secretNonce4 = secretNonceGenerator4.GetScalar();

			Assert.NotEqual(secretNonce1, secretNonce2);
			Assert.NotEqual(secretNonce1, secretNonce3);
			Assert.NotEqual(secretNonce1, secretNonce4);

			Assert.NotEqual(secretNonce2, secretNonce3);
			Assert.NotEqual(secretNonce2, secretNonce4);

			// Since transcript3 and transcript4 share the same public inputs and
			// witness, with no randomness they should be identical
			Assert.Equal(secretNonce3, secretNonce4);
		}

		[Theory]
		[InlineData(1)]
		[InlineData(3)]
		[InlineData(5)]
		[InlineData(7)]
		public void SyntheticNoncesVectorTest(int size)
		{
			var protocol = Encoding.UTF8.GetBytes("witness size");

			var rnd = new SecureRandom();

			var witness = new Scalar[size];

			var transcript = new Transcript(protocol);
			var secretNonceProvider = transcript.CreateSyntheticSecretNonceProvider(witness, rnd);

			var secretNonce = secretNonceProvider.Sequence.First();

			Assert.Equal(secretNonce.Count(), witness.Length );
		}

		[Fact]
		public void SyntheticNoncesThrows()
		{
			var protocol = Encoding.UTF8.GetBytes("empty witness not allowed");

			var rnd = new SecureRandom();

			var transcript = new Transcript(protocol);

			Assert.ThrowsAny<ArgumentException>(() => transcript.CreateSyntheticSecretNonceProvider(new Scalar[0], rnd));
		}
	}
}
