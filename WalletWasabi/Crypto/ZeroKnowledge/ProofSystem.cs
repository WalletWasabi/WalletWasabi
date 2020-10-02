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

		public static Knowledge ShowCredential(CredentialPresentation presentation, Scalar z, Credential credential, CoordinatorParameters iparams)
			=> new Knowledge(
				ShowCredential(presentation, z * iparams.I, iparams),
				new ScalarVector(z, (credential.Mac.T * z).Negate(), credential.Mac.T, credential.Amount, credential.Randomness));

		public static Statement ShowCredential(CredentialPresentation c, GroupElement Z, CoordinatorParameters iparams)
			=> new Statement(new GroupElement[,]
			{
				// public                     Witness terms:
				// point      z               z0              t               a               r
				{ Z,          iparams.I,      O,              O,              O,              O },
				{ c.Cx1,      Generators.Gx1, Generators.Gx0, c.Cx0,          O,              O },
				{ c.Ca,       Generators.Ga,  O,              O,              Generators.Gg,  Generators.Gh },
				{ c.S,        O,              O,              O,              O,              Generators.Gs }
			});

		public static Knowledge BalanceProof(Scalar zSum, Scalar rDeltaSum)
			=> new Knowledge(BalanceProof(zSum * Generators.Ga + rDeltaSum * Generators.Gh), new ScalarVector(zSum, rDeltaSum));

		// Balance commitment must be a commitment to 0, with randomness in Gh and
		// additional randomness from attribute randomization in Show protocol,
		// using generator Ga. Witness terms: (\sum z, \sum r_i - r'_i)
		public static Statement BalanceProof(GroupElement balanceCommitment)
			=> new Statement(balanceCommitment, Generators.Ga, Generators.Gh);
	}
}
