using System;
using System.Linq;
using NBitcoin.Secp256k1;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Crypto.ZeroKnowledge.LinearRelation;
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
		public void CanProveAndVerifySerialNumbers()
		{
			var rnd = new SecureRandom();
			var z = rnd.GetScalar();
			var r = rnd.GetScalar();
			var a = rnd.GetScalar();

			static (GroupElement Ca, GroupElement S) SerialNumberPublicPoints(Scalar z, Scalar a, Scalar r) =>
				(z * Generators.Ga + r * Generators.Gh + a * Generators.Gg, r * Generators.Gs);

			{
				var witness = new ScalarVector(z, a, r);
				var (Ca, S) = SerialNumberPublicPoints(z, a, r);
				var statement = ProofSystem.SerialNumber(Ca, S);
				var proofOfSerialNumber = ProofSystem.Prove(new Knowledge(statement, witness), rnd);

				var isValidProof = ProofSystem.Verify(statement, proofOfSerialNumber);
				Assert.True(isValidProof);
			}

			// Test zero amount
			a = Scalar.Zero;
			{
				var witness = new ScalarVector(z, a, r);
				var (Ca, S) = SerialNumberPublicPoints(z, a, r);
				var statement = ProofSystem.SerialNumber(Ca, S);
				var proofOfSerialNumber = ProofSystem.Prove(new Knowledge(statement, witness), rnd);

				var isValidProof = ProofSystem.Verify(statement, proofOfSerialNumber);
				Assert.True(isValidProof);
			}
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
			// coordinator's secret key. The coordinator sends the pair (MAC + proofOfMac) back
			// to the client.
			var t = rnd.GetScalar();
			var mac = MAC.ComputeMAC(coordinatorKey, Ma, t);

			// The client randomizes the commitments before presenting the them to the coordinator
			// proves to the coordinator that a credential is valid
			var z = rnd.GetScalar();
			var (knowledge, randomizedCommitments) = ProofSystem.MacShow(coordinatorParameters, mac, z, amount, r);
			var proofOfMacShow = ProofSystem.Prove(knowledge, rnd);

			// The coordinator must verify the received "randomized" credential is valid.
			var Z = ProofSystem.ComputeZ(randomizedCommitments, coordinatorKey);
			Assert.Equal(Z, z * coordinatorParameters.I);

			var statement = ProofSystem.MacShow(coordinatorParameters, Z, randomizedCommitments.Cx0, randomizedCommitments.Cx1);
			var isValidProof = ProofSystem.Verify(statement, proofOfMacShow);

			Assert.True(isValidProof);
		}
	}
}
