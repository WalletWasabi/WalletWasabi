using System;
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

			// A blinded amout is known as an `attribute`. In this case the attribute Ma is the 
			// valued 10000 blinded with a random `blindingFactor`. This attribute is sent to 
			// the coordinator.
			var amount = new Scalar(10_000);
			var blindingFactor = rnd.GetScalar();
			var Ma = amount * Generators.G + blindingFactor * Generators.Gh;

			// The coordinator generates a MAC and a proof that that MAC was generated using the 
			// coordinator's secret key. The coordinator sends the pair (MAC + proofOfMac) back 
			// to the client.
			var t = rnd.GetScalar();
			var mac = MAC.ComputeMAC(coordinatorKey, Ma, t);

			var coordinatorStatement = ProofSystem.CreateStatement(coordinatorParameters, mac.V, Ma, t);
			var proverBuilder = ProofSystem.CreateProver(coordinatorStatement, coordinatorKey);
			var macProver = proverBuilder(rnd);
			var proofOfMac = macProver();

			// The client receives the MAC and the proofOfMac which let the client know that the MAC 
			// was generated with the coordinator's secret key.
			var clientStatement = ProofSystem.CreateStatement(coordinatorParameters, mac.V, Ma, mac.T);
			var verifierBuilder = ProofSystem.CreateVerifier(clientStatement);
			var macVerifier = verifierBuilder(proofOfMac);
			var isValidProof = macVerifier();

			Assert.True(isValidProof);
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanProveAndVerifyNULL()
		{
			// The client wants to request a zero amount credential and it needs to prove
			// that the blinded amount is indeed zero.  
			var rnd = new SecureRandom();
			var amount = Scalar.Zero;
			var blindingFactor = rnd.GetScalar();
			var Ma = amount * Generators.Gg + blindingFactor * Generators.Gh;

			var clientStatement = ProofSystem.CreateStatement(Ma, Generators.Gh);
			var proverBuilder = ProofSystem.CreateProver(clientStatement, blindingFactor);
			var nullProver = proverBuilder(rnd);
			var proofOfNull = nullProver();

			// The coordinator must verify the blinded amount is zero
			var coordinatorStatement = ProofSystem.CreateStatement(Ma, Generators.Gh);
			var verifierBuilder = ProofSystem.CreateVerifier(coordinatorStatement);
			var nullVerifier = verifierBuilder(proofOfNull);
			var isValidProof = nullVerifier();

			Assert.True(isValidProof);
		}
	}
}
