using NBitcoin.Secp256k1;
using System;
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

		public static Knowledge ShowCredential(CoordinatorParameters iparams, RandomizedCommitments c, Scalar z, Scalar t, Scalar a, Scalar r)
			=> new Knowledge(ShowCredential(iparams, z * iparams.I, c), new ScalarVector(z, (t * z).Negate(), t, a, r));

		public static Statement ShowCredential(CoordinatorParameters iparams, GroupElement Z, RandomizedCommitments c)
			=> new Statement(new GroupElement[,]
			{
				// public                     Witness terms:
				// point      z               z0              t               a               r
				{ Z,          iparams.I,      O,              O,              O,              O },
				{ c.Cx1,      Generators.Gx1, Generators.Gx0, c.Cx0,          O,              O }, // Cx1 = z*Gx1 + t*U, z0 cancels t*z*Gx0 out of t*Cx0 leaving z*Gx1 + t*U
				{ c.Ca,       Generators.Ga,  O,              O,              Generators.Gg,  Generators.Gh },
				{ c.S,        O,              O,              O,              O,              Generators.Gs }
			});
	}
}
