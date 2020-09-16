using NBitcoin.Secp256k1;
using System.Linq;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge.LinearRelation;
using WalletWasabi.Crypto.ZeroKnowledge.NonInteractive;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	public static class ProofSystem
	{
		private static GroupElement O = GroupElement.Infinity;

		public static bool Verify(Statement statement, Proof proof)
		{
			return Verifier.Verify(new Transcript(new byte[0]), new[] { statement }, new[] { proof });
		}

		public static Proof Prove(Knowledge knowledge, WasabiRandom random)
		{
			return Prover.Prove(new Transcript(new byte[0]), new[] { knowledge }, random).First();
		}

		// Syntactic sugar used in tests
		public static Proof Prove(Statement statement, Scalar witness, WasabiRandom random)
			=> Prove(statement, new ScalarVector(witness), random);

		public static Proof Prove(Statement statement, ScalarVector witness, WasabiRandom random)
			=> Prove(new Knowledge(statement, witness), random);

		public static Knowledge IssuerParameters(MAC mac, GroupElement ma, CoordinatorSecretKey sk)
			=> new Knowledge(IssuerParameters(sk.ComputeCoordinatorParameters(), mac, ma), new ScalarVector(sk.W, sk.Wp, sk.X0, sk.X1, sk.Ya));

		public static Statement IssuerParameters(CoordinatorParameters iparams, MAC mac, GroupElement ma)
			=> new Statement(new GroupElement[,]
			{
				// public                                             Witness terms:
				// point                     w,             wp,             x0,             x1,             ya
				{ mac.V,                     Generators.Gw, O,              mac.U,          mac.T * mac.U,  ma },
				{ Generators.GV - iparams.I, O,             O,              Generators.Gx0, Generators.Gx1, Generators.Ga },
				{ iparams.Cw,                Generators.Gw, Generators.Gwp, O,              O,              O },
			});

		public static Knowledge SerialNumber(GroupElement Ca, GroupElement S, Scalar z, Scalar a, Scalar r)
			=> new Knowledge(SerialNumber(Ca, S), new ScalarVector(z, a, r));

		public static Statement SerialNumber(GroupElement Ca, GroupElement S)
			=> new Statement(new GroupElement[,]
			{
				// public                    Witness terms:
				// point      z              a               r
				{ Ca,         Generators.Ga, Generators.Gg,  Generators.Gh },
				{ S,          O,             O,              Generators.Gs }
			});

		public static (Knowledge knowledge, RandomizedCommitments randomizedCommitments) MacShow(CoordinatorParameters iparams, MAC mac, Scalar z, Scalar a, Scalar r)
		{
			var Ca = z * Generators.Ga + a * Generators.Gg + r * Generators.Gh;
			var Cx0 = z * Generators.Gx0 + mac.U;
			var Cx1 = z * Generators.Gx1 + mac.T * mac.U;
			var CV = z * Generators.GV + mac.V;
			var Z = z * iparams.I;

			return (new Knowledge(MacShow(iparams, Z, Cx0, Cx1), new ScalarVector(z, (mac.T * z).Negate(), mac.T)), new RandomizedCommitments(Ca, Cx0, Cx1, CV));
		}

		public static Statement MacShow(CoordinatorParameters iparams, GroupElement Z, GroupElement Cx0, GroupElement Cx1)
			=> new Statement(new GroupElement[,]
			{
				// public                     Witness terms:
				// point      z               z0              t
				{ Z,          iparams.I,      O,              O },
				{ Cx1,        Generators.Gx1, Generators.Gx0, Cx0 }, // Cx1 = z*Gx1 + t*U, z0 cancels t*z*Gx0 out of t*Cx0 leaving z*Gx1 + t*U
			});

		public static GroupElement ComputeZ(RandomizedCommitments rc, CoordinatorSecretKey sk)
			=> rc.CV - (sk.W * Generators.Gw + sk.X0 * rc.Cx0 + sk.X1 * rc.Cx1 + sk.Ya * rc.Ca);
	}
}
