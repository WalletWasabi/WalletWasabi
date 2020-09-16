using NBitcoin.Secp256k1;
using System.Linq;
using System.Collections.Generic;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge.LinearRelation;
using WalletWasabi.Crypto.ZeroKnowledge.NonInteractive;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public static class ProofSystem
	{
		private static GroupElement O = GroupElement.Infinity;

		public static bool Verify(LinearRelation.Statement statement, Proof proof)
				=> NonInteractive.Verifier.Verify(new Transcript(new byte[0]), new[] { statement }, new[] { proof });

		public static Proof Prove(Knowledge knowledge, WasabiRandom random)
			=> NonInteractive.Prover.Prove(new Transcript(new byte[0]), new[] { knowledge }, random).First();

		// Syntactic sugar used in tests
		public static Proof Prove(LinearRelation.Statement statement, Scalar witness, WasabiRandom random)
			=> Prove(statement, new ScalarVector(witness), random);

		public static Proof Prove(LinearRelation.Statement statement, ScalarVector witness, WasabiRandom random)
			=> Prove(new Knowledge(statement, witness), random);

		public static Knowledge IssuerParameters(MAC mac, GroupElement ma, CoordinatorSecretKey sk)
			=> new Knowledge(IssuerParameters(sk.ComputeCoordinatorParameters(), mac, ma), new ScalarVector(sk.W, sk.Wp, sk.X0, sk.X1, sk.Ya));

		public static LinearRelation.Statement IssuerParameters(CoordinatorParameters iparams, MAC mac, GroupElement ma)
		{
			return new LinearRelation.Statement(new GroupElement[,]
			{
				// public                                             Witness terms:
				// point                     w,             wp,             x0,             x1,             ya
				{ mac.V,                     Generators.Gw, O,              mac.U,          mac.T * mac.U,  ma },
				{ Generators.GV - iparams.I, O,             O,              Generators.Gx0, Generators.Gx1, Generators.Ga },
				{ iparams.Cw,                Generators.Gw, Generators.Gwp, O,              O,              O },
			});
		}
	}
}
