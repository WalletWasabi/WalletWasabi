using System;
using System.Linq;
using NBitcoin.Secp256k1;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Crypto.ZeroKnowledge.NonInteractive;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto
{
	public class ProofSystemTests
	{
		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanProveAndVerifyMAC()
		{
			// The coordinator generates a composed private key called CoordinatorSecretKey
			// and derives from that the coordinator's public parameters called CoordinatorParameters.
			var rnd = new SecureRandom();
			var coordinatorKey = new CoordinatorSecretKey(rnd);
			var coordinatorParameters = coordinatorKey.ComputeCoordinatorParameters();

			// A blinded amount is known as an `attribute`. In this case the attribute Ma is the
			// value 10000 blinded with a random `blindingFactor`. This attribute is sent to
			// the coordinator.
			var amount = new Scalar(10_000);
			var r = rnd.GetScalar();
			var Ma = amount * Generators.G + r * Generators.Gh;

			// The coordinator generates a MAC and a proof that the MAC was generated using the
			// coordinator's secret key. The coordinator sends the pair (MAC + proofOfMac) back
			// to the client.
			var t = rnd.GetScalar();
			var mac = MAC.ComputeMAC(coordinatorKey, Ma, t);

			var coordinatorKnowledge = ProofSystem.IssuerParameters(mac, Ma, coordinatorKey);
			var proofOfMac = ProofSystem.Prove(coordinatorKnowledge, rnd);

			// The client receives the MAC and the proofOfMac which let the client know that the MAC
			// was generated with the coordinator's secret key.
			var clientStatement = ProofSystem.IssuerParameters(coordinatorParameters, mac, Ma);
			var isValidProof = ProofSystem.Verify(clientStatement, proofOfMac);
			Assert.True(isValidProof);

			var corruptedResponses = new ScalarVector(proofOfMac.Responses.Reverse());
			var invalidProofOfMac = new Proof(proofOfMac.PublicNonces, corruptedResponses);
			isValidProof = ProofSystem.Verify(clientStatement, invalidProofOfMac);
			Assert.False(isValidProof);

			var corruptedPublicNonces = new GroupElementVector(proofOfMac.PublicNonces.Reverse());
			invalidProofOfMac = new Proof(corruptedPublicNonces, proofOfMac.Responses);
			isValidProof = ProofSystem.Verify(clientStatement, invalidProofOfMac);
			Assert.False(isValidProof);
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanProveAndVerifyMacShow()
		{
			var rnd = new SecureRandom();
			var coordinatorKey = new CoordinatorSecretKey(rnd);
			var coordinatorParameters = coordinatorKey.ComputeCoordinatorParameters();

			// A blinded amount is known as an `attribute`. In this case the attribute Ma is the
			// value 10000 blinded with a random `blindingFactor`. This attribute is sent to
			// the coordinator.
			var amount = new Scalar(10_000);
			var r = rnd.GetScalar();
			var Ma = amount * Generators.Gg + r * Generators.Gh;

			// The coordinator generates a MAC and a proof that the MAC was generated using the
			// coordinator's secret key. The coordinator sends the pair (MAC, proofOfMac) back
			// to the client.
			var t = rnd.GetScalar();
			var mac = MAC.ComputeMAC(coordinatorKey, Ma, t);

			// The client randomizes the commitments before presenting them to the coordinator proving to
			// the coordinator that a credential is valid (prover knows a valid MAC on non-randomized attribute)
			var credential = new Credential(amount, r, mac, Ma);
			var z = rnd.GetScalar();
			var randomizedCredential = credential.Present(z);
			var knowledge = ProofSystem.ShowCredential(randomizedCredential, z, credential, coordinatorParameters);
			var proofOfMacShow = ProofSystem.Prove(knowledge, rnd);

			// The coordinator must verify the received randomized credential is valid.
			var Z = randomizedCredential.ComputeZ(coordinatorKey);
			Assert.Equal(Z, z * coordinatorParameters.I);

			var statement = ProofSystem.ShowCredential(randomizedCredential, Z, coordinatorParameters);
			var isValidProof = ProofSystem.Verify(statement, proofOfMacShow);

			Assert.True(isValidProof);
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanProveAndVerifyPresentedBalance()
		{
			var rnd = new SecureRandom();

			var a = new Scalar(10_000u);
			var r = rnd.GetScalar();
			var z = rnd.GetScalar();
			var Ca = z * Generators.Ga + a * Generators.Gg + r * Generators.Gh;
			var delta = a.Negate();

			var knowledge = ProofSystem.BalanceProof(z, r);
			var proofOfBalance = ProofSystem.Prove(knowledge, rnd);

			var statement = ProofSystem.BalanceProof(Ca - a * Generators.Gg);
			Assert.True(ProofSystem.Verify(statement, proofOfBalance));

			var badStatement = ProofSystem.BalanceProof(Ca + Generators.Gg - a * Generators.Gg);
			Assert.False(ProofSystem.Verify(badStatement, proofOfBalance));

			badStatement = ProofSystem.BalanceProof(Ca);
			Assert.False(ProofSystem.Verify(badStatement, proofOfBalance));
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanProveAndVerifyRequestedBalance()
		{
			var rnd = new SecureRandom();

			var a = new Scalar(10_000u);
			var r = rnd.GetScalar();
			var Ma = a * Generators.Gg + r * Generators.Gh;

			var delta = a;
			var knowledge = ProofSystem.BalanceProof(Scalar.Zero, r.Negate());
			var proofOfBalance = ProofSystem.Prove(knowledge, rnd);

			var statement = ProofSystem.BalanceProof(a * Generators.Gg - Ma);
			Assert.True(ProofSystem.Verify(statement, proofOfBalance));

			var badDelta = delta + Scalar.One;
			var badStatement = ProofSystem.BalanceProof(Ma);
			Assert.False(ProofSystem.Verify(badStatement, proofOfBalance));
		}

		[Theory]
		[InlineData(0, 0)]
		[InlineData(0, 1)]
		[InlineData(1, 0)]
		[InlineData(1, 1)]
		[InlineData(7, 11)]
		[InlineData(11, 7)]
		[InlineData(10_000, 0)]
		[InlineData(0, 10_000)]
		[InlineData(10_000, 10_000)]
		[InlineData(int.MaxValue, int.MaxValue)]
		[InlineData(int.MaxValue - 1, int.MaxValue)]
		[InlineData(int.MaxValue, int.MaxValue - 1)]
		[Trait("UnitTest", "UnitTest")]
		public void CanProveAndVerifyBalance(int presentedAmount, int requestedAmount)
		{
			var rnd = new SecureRandom();

			var a = new Scalar((uint)presentedAmount);
			var r = rnd.GetScalar();
			var z = rnd.GetScalar();
			var Ca = z * Generators.Ga + a * Generators.Gg + r * Generators.Gh;

			var ap = new Scalar((uint)requestedAmount);
			var rp = rnd.GetScalar();
			var Ma = ap * Generators.Gg + rp * Generators.Gh;

			var delta = new Scalar((uint)Math.Abs(presentedAmount - requestedAmount));
			delta = presentedAmount > requestedAmount ? delta.Negate() : delta;
			var knowledge = ProofSystem.BalanceProof(z, r + rp.Negate());

			var proofOfBalance = ProofSystem.Prove(knowledge, rnd);

			var statement = ProofSystem.BalanceProof(Ca + delta * Generators.Gg - Ma);
			Assert.True(ProofSystem.Verify(statement, proofOfBalance));

			var badStatement = ProofSystem.BalanceProof(Ca  + (delta + Scalar.One) * Generators.Gg - Ma);
			Assert.False(ProofSystem.Verify(badStatement, proofOfBalance));
		}

		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		[InlineData(2)]
		// Slow tests
		[InlineData(3)]
		[InlineData(4)]
		[InlineData(int.MaxValue)]
		[InlineData((uint)int.MaxValue + 1)]
		[InlineData(uint.MaxValue - 1)]
		[InlineData(uint.MaxValue)]
		// TODO [InlineData(uint.MaxValue + 1)]
		public void CanProveAndVerifyCommitmentRange(ulong amount)
		{
			var rnd = new SecureRandom();

			var amountScalar = new Scalar(amount);
			var randomness = rnd.GetScalar();
			var fullCommitment = amountScalar * Generators.Gg + randomness * Generators.Gh;

			foreach (int width in new[] { 0, 1, 2, 3, 8, 64 })
			// Slow tests foreach (int width in new[] { 0, 1, 2, 3, 8, 30, 31, 32, 50})
			{
				var maskedAmount = amount & ((1ul << width) - 1);

				if (maskedAmount == amount || maskedAmount != 0)
				{
					var maskedScalar = new Scalar(maskedAmount);
					var maskedCommitment = maskedScalar * Generators.Gg + randomness * Generators.Gh;
					var (knowledge, bitCommitments) = ProofSystem.RangeProof(maskedScalar, randomness, width, rnd);

					var rangeProof = ProofSystem.Prove(knowledge, rnd);

					Assert.True(ProofSystem.Verify(ProofSystem.RangeProof(maskedCommitment, bitCommitments), rangeProof));

					if (maskedAmount != amount)
					{
						Assert.False(ProofSystem.Verify(ProofSystem.RangeProof(fullCommitment, bitCommitments), rangeProof));
					}
				}
			}
		}
	}
}
