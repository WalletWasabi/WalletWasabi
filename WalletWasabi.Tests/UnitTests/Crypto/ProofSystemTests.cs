using System;
using System.Linq;
using NBitcoin.Secp256k1;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Crypto
{
	public class ProofSystemTests
	{
		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanProveAndVerifyMAC()
		{
			// The coordinator generates a composed private key called CredentialIssuerSecretKey
			// and derives from that the coordinator's public parameters called credentialIssuerParameters.
			using var rnd = new SecureRandom();
			var credentialIssuerKey = new CredentialIssuerSecretKey(rnd);
			var credentialIssuerParameters = credentialIssuerKey.ComputeCredentialIssuerParameters();

			// A blinded amount is known as an `attribute`. In this case the attribute Ma is the
			// value 10000 blinded with a random `blindingFactor`. This attribute is sent to
			// the coordinator.
			var amount = new Scalar(10_000);
			var r = rnd.GetScalar();
			var ma = amount * Generators.G + r * Generators.Gh;

			// The coordinator generates a MAC and a proof that the MAC was generated using the
			// coordinator's secret key. The coordinator sends the pair (MAC + proofOfMac) back
			// to the client.
			var t = rnd.GetScalar();
			var mac = MAC.ComputeMAC(credentialIssuerKey, ma, t);

			var coordinatorKnowledge = ProofSystem.IssuerParametersKnowledge(mac, ma, credentialIssuerKey);
			var proofOfMac = ProofSystemHelpers.Prove(coordinatorKnowledge, rnd);

			// The client receives the MAC and the proofOfMac which let the client know that the MAC
			// was generated with the coordinator's secret key.
			var clientStatement = ProofSystem.IssuerParametersStatement(credentialIssuerParameters, mac, ma);
			var isValidProof = ProofSystemHelpers.Verify(clientStatement, proofOfMac);
			Assert.True(isValidProof);

			var corruptedResponses = new ScalarVector(proofOfMac.Responses.Reverse());
			var invalidProofOfMac = new Proof(proofOfMac.PublicNonces, corruptedResponses);
			isValidProof = ProofSystemHelpers.Verify(clientStatement, invalidProofOfMac);
			Assert.False(isValidProof);

			var corruptedPublicNonces = new GroupElementVector(proofOfMac.PublicNonces.Reverse());
			invalidProofOfMac = new Proof(corruptedPublicNonces, proofOfMac.Responses);
			isValidProof = ProofSystemHelpers.Verify(clientStatement, invalidProofOfMac);
			Assert.False(isValidProof);
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanProveAndVerifyMacShow()
		{
			using var rnd = new SecureRandom();
			var credentialIssuerKey = new CredentialIssuerSecretKey(rnd);
			var credentialIssuerParameters = credentialIssuerKey.ComputeCredentialIssuerParameters();

			// A blinded amount is known as an `attribute`. In this case the attribute Ma is the
			// value 10000 blinded with a random `blindingFactor`. This attribute is sent to
			// the coordinator.
			var amount = new Scalar(10_000);
			var r = rnd.GetScalar();
			var ma = amount * Generators.Gg + r * Generators.Gh;

			// The coordinator generates a MAC and a proof that the MAC was generated using the
			// coordinator's secret key. The coordinator sends the pair (MAC, proofOfMac) back
			// to the client.
			var t = rnd.GetScalar();
			var mac = MAC.ComputeMAC(credentialIssuerKey, ma, t);

			// The client randomizes the commitments before presenting them to the coordinator proving to
			// the coordinator that a credential is valid (prover knows a valid MAC on non-randomized attribute)
			var credential = new Credential(amount, r, mac);
			var z = rnd.GetScalar();
			var randomizedCredential = credential.Present(z);
			var knowledge = ProofSystem.ShowCredentialKnowledge(randomizedCredential, z, credential, credentialIssuerParameters);
			var proofOfMacShow = ProofSystemHelpers.Prove(knowledge, rnd);

			// The coordinator must verify the received randomized credential is valid.
			var capitalZ = randomizedCredential.ComputeZ(credentialIssuerKey);
			Assert.Equal(capitalZ, z * credentialIssuerParameters.I);

			var statement = ProofSystem.ShowCredentialStatement(randomizedCredential, capitalZ, credentialIssuerParameters);
			var isValidProof = ProofSystemHelpers.Verify(statement, proofOfMacShow);

			Assert.True(isValidProof);
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanProveAndVerifyPresentedBalance()
		{
			using var rnd = new SecureRandom();

			var a = new Scalar(10_000u);
			var r = rnd.GetScalar();
			var z = rnd.GetScalar();
			var ca = z * Generators.Ga + a * Generators.Gg + r * Generators.Gh;

			var knowledge = ProofSystem.BalanceProofKnowledge(z, r);
			var proofOfBalance = ProofSystemHelpers.Prove(knowledge, rnd);

			var statement = ProofSystem.BalanceProofStatement(ca - a * Generators.Gg);
			Assert.True(ProofSystemHelpers.Verify(statement, proofOfBalance));

			var badStatement = ProofSystem.BalanceProofStatement(ca + Generators.Gg - a * Generators.Gg);
			Assert.False(ProofSystemHelpers.Verify(badStatement, proofOfBalance));

			badStatement = ProofSystem.BalanceProofStatement(ca);
			Assert.False(ProofSystemHelpers.Verify(badStatement, proofOfBalance));
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanProveAndVerifyRequestedBalance()
		{
			using var rnd = new SecureRandom();

			var a = new Scalar(10_000u);
			var r = rnd.GetScalar();
			var ma = a * Generators.Gg + r * Generators.Gh;

			var knowledge = ProofSystem.BalanceProofKnowledge(Scalar.Zero, r.Negate());
			var proofOfBalance = ProofSystemHelpers.Prove(knowledge, rnd);

			var statement = ProofSystem.BalanceProofStatement(a * Generators.Gg - ma);
			Assert.True(ProofSystemHelpers.Verify(statement, proofOfBalance));

			var badStatement = ProofSystem.BalanceProofStatement(ma);
			Assert.False(ProofSystemHelpers.Verify(badStatement, proofOfBalance));
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
			using var rnd = new SecureRandom();

			var a = new Scalar((uint)presentedAmount);
			var r = rnd.GetScalar();
			var z = rnd.GetScalar();
			var ca = z * Generators.Ga + a * Generators.Gg + r * Generators.Gh;

			var ap = new Scalar((uint)requestedAmount);
			var rp = rnd.GetScalar();
			var ma = ap * Generators.Gg + rp * Generators.Gh;

			var delta = new Scalar((uint)Math.Abs(presentedAmount - requestedAmount));
			delta = presentedAmount > requestedAmount ? delta.Negate() : delta;
			var knowledge = ProofSystem.BalanceProofKnowledge(z, r + rp.Negate());

			var proofOfBalance = ProofSystemHelpers.Prove(knowledge, rnd);

			var statement = ProofSystem.BalanceProofStatement(ca + delta * Generators.Gg - ma);
			Assert.True(ProofSystemHelpers.Verify(statement, proofOfBalance));

			var badStatement = ProofSystem.BalanceProofStatement(ca + (delta + Scalar.One) * Generators.Gg - ma);
			Assert.False(ProofSystemHelpers.Verify(badStatement, proofOfBalance));
		}

		[Theory]
		[InlineData(0, 0, true)]
		[InlineData(0, 1, true)]
		[InlineData(1, 0, false)]
		[InlineData(1, 1, true)]
		[InlineData(1, 2, true)]
		[InlineData(2, 0, false)]
		[InlineData(2, 1, false)]
		[InlineData(2, 2, true)]
		[InlineData(3, 1, false)]
		[InlineData(3, 2, true)]
		[InlineData(4, 2, false)]
		[InlineData(4, 3, true)]
		[InlineData(7, 2, false)]
		[InlineData(7, 3, true)]
		[InlineData((ulong)uint.MaxValue + 1, 32, false)]
		[InlineData((ulong)uint.MaxValue + 1, 33, true)]
		public void CanProveAndVerifyCommitmentRange(ulong amount, int width, bool pass)
		{
			using var rnd = new SecureRandom();

			var amountScalar = new Scalar(amount);
			var randomness = rnd.GetScalar();
			var commitment = amountScalar * Generators.Gg + randomness * Generators.Gh;

			// First, generate a proof for the given statement. This proof may
			// be invalid (verification equation fails to hold) if the statement
			// is in fact wrong, in which case the verifier should reject.
			var (knowledge, bitCommitments) = ProofSystem.RangeProofKnowledge(amountScalar, randomness, width, rnd);

			var rangeProof = ProofSystemHelpers.Prove(knowledge, rnd);

			Assert.Equal(pass, ProofSystemHelpers.Verify(ProofSystem.RangeProofStatement(commitment, bitCommitments, width), rangeProof));

			if (!pass)
			{
				Assert.Throws<ArgumentException>(() => knowledge.AssertSoundness());

				// When the statement is unprovable, modify the secret input by
				// clearing the high bits to make sure that the proof is always
				// formally valid, but when the original statement is false this
				// will be a valid proof of a different statement. The verifier
				// should still reject.
				var maskedScalar = new Scalar(amount & ((1ul << width) - 1));

				var (knowledgeOfSomethingElse, incompleteBitCommitments) = ProofSystem.RangeProofKnowledge(maskedScalar, randomness, width, rnd);

				var incorrectRangeProof = ProofSystemHelpers.Prove(knowledgeOfSomethingElse, rnd);

				Assert.False(ProofSystemHelpers.Verify(ProofSystem.RangeProofStatement(commitment, incompleteBitCommitments, width), incorrectRangeProof));

				// For completeness, make sure other corrupted statements are also rejected
				Assert.False(ProofSystemHelpers.Verify(ProofSystem.RangeProofStatement(commitment, bitCommitments, width), incorrectRangeProof));
				Assert.False(ProofSystemHelpers.Verify(ProofSystem.RangeProofStatement(commitment, incompleteBitCommitments, width), rangeProof));
			}
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanProveAndVerifyZeroProofs()
		{
			using var rnd = new SecureRandom();

			var a0 = Scalar.Zero;
			var r0 = rnd.GetScalar();
			var ma0 = a0 * Generators.Gg + r0 * Generators.Gh;

			var a1 = Scalar.Zero;
			var r1 = rnd.GetScalar();
			var ma1 = a1 * Generators.Gg + r1 * Generators.Gh;

			var knowledge = new[]
			{
				ProofSystem.ZeroProofKnowledge(ma0, r0),
				ProofSystem.ZeroProofKnowledge(ma1, r1)
			};

			var proofs = ProofSystem.Prove(new Transcript(Array.Empty<byte>()), knowledge, rnd);

			var statements = new[]
			{
				ProofSystem.ZeroProofStatement(ma0),
				ProofSystem.ZeroProofStatement(ma1)
			};

			Assert.True(ProofSystem.Verify(new Transcript(Array.Empty<byte>()), statements, proofs));
		}
	}
}
