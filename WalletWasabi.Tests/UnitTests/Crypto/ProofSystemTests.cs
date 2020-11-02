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
			var proofOfMac = ProofSystemHelpers.Prove(coordinatorKnowledge, rnd);

			// The client receives the MAC and the proofOfMac which let the client know that the MAC
			// was generated with the coordinator's secret key.
			var clientStatement = ProofSystem.IssuerParametersStmt(coordinatorParameters, mac, Ma);
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
			var credential = new Credential(amount, r, mac);
			var z = rnd.GetScalar();
			var randomizedCredential = credential.Present(z);
			var knowledge = ProofSystem.ShowCredential(randomizedCredential, z, credential, coordinatorParameters);
			var proofOfMacShow = ProofSystemHelpers.Prove(knowledge, rnd);

			// The coordinator must verify the received randomized credential is valid.
			var Z = randomizedCredential.ComputeZ(coordinatorKey);
			Assert.Equal(Z, z * coordinatorParameters.I);

			var statement = ProofSystem.ShowCredentialStmt(randomizedCredential, Z, coordinatorParameters);
			var isValidProof = ProofSystemHelpers.Verify(statement, proofOfMacShow);

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

			var knowledge = ProofSystem.BalanceProof(z, r);
			var proofOfBalance = ProofSystemHelpers.Prove(knowledge, rnd);

			var statement = ProofSystem.BalanceProofStmt(Ca - a * Generators.Gg);
			Assert.True(ProofSystemHelpers.Verify(statement, proofOfBalance));

			var badStatement = ProofSystem.BalanceProofStmt(Ca + Generators.Gg - a * Generators.Gg);
			Assert.False(ProofSystemHelpers.Verify(badStatement, proofOfBalance));

			badStatement = ProofSystem.BalanceProofStmt(Ca);
			Assert.False(ProofSystemHelpers.Verify(badStatement, proofOfBalance));
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanProveAndVerifyRequestedBalance()
		{
			var rnd = new SecureRandom();

			var a = new Scalar(10_000u);
			var r = rnd.GetScalar();
			var Ma = a * Generators.Gg + r * Generators.Gh;

			var knowledge = ProofSystem.BalanceProof(Scalar.Zero, r.Negate());
			var proofOfBalance = ProofSystemHelpers.Prove(knowledge, rnd);

			var statement = ProofSystem.BalanceProofStmt(a * Generators.Gg - Ma);
			Assert.True(ProofSystemHelpers.Verify(statement, proofOfBalance));

			var badStatement = ProofSystem.BalanceProofStmt(Ma);
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

			var proofOfBalance = ProofSystemHelpers.Prove(knowledge, rnd);

			var statement = ProofSystem.BalanceProofStmt(Ca + delta * Generators.Gg - Ma);
			Assert.True(ProofSystemHelpers.Verify(statement, proofOfBalance));

			var badStatement = ProofSystem.BalanceProofStmt(Ca + (delta + Scalar.One) * Generators.Gg - Ma);
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
			var rnd = new SecureRandom();

			var amountScalar = new Scalar(amount);
			var randomness = rnd.GetScalar();
			var commitment = amountScalar * Generators.Gg + randomness * Generators.Gh;

			var maskedScalar = new Scalar(amount & ((1ul << width) - 1));
			var (knowledge, bitCommitments) = ProofSystem.RangeProof(maskedScalar, randomness, width, rnd);

			var rangeProof = ProofSystemHelpers.Prove(knowledge, rnd);

			Assert.Equal(pass, ProofSystemHelpers.Verify(ProofSystem.RangeProofStmt(commitment, bitCommitments), rangeProof));

			if (!pass)
			{
				Assert.Throws<ArgumentException>(() => ProofSystem.RangeProof(amountScalar, randomness, width, rnd));
			}
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanProveAndVerifyZeroProofs()
		{
			var rnd = new SecureRandom();

			var a0 = Scalar.Zero;
			var r0 = rnd.GetScalar();
			var Ma0 = a0 * Generators.Gg + r0 * Generators.Gh;

			var a1 = Scalar.Zero;
			var r1 = rnd.GetScalar();
			var Ma1 = a1 * Generators.Gg + r1 * Generators.Gh;

			var knowledge = new[]
			{
				ProofSystem.ZeroProof(Ma0, r0),
				ProofSystem.ZeroProof(Ma1, r1)
			};

			var proofs = ProofSystem.Prove(new Transcript(new byte[0]), knowledge, rnd);

			var statements = new[]
			{
				ProofSystem.ZeroProofStmt(Ma0),
				ProofSystem.ZeroProofStmt(Ma1)
			};

			Assert.True(ProofSystem.Verify(new Transcript(new byte[0]), statements, proofs));
		}
	}
}
