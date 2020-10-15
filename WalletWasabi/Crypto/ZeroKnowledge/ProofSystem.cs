using NBitcoin.Secp256k1;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge.LinearRelation;
using WalletWasabi.Crypto.ZeroKnowledge.NonInteractive;
using WalletWasabi.Helpers;

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

		// overload for bootstrap credential request proofs.
		// this is just a range proof with width=0
		// equivalent to proof of representation w/ Gh
		public static Knowledge ZeroProof(GroupElement ma, Scalar r)
			=> new Knowledge(ZeroProof(ma), new ScalarVector(r));

		// TODO swap return value order, remove GroupElement argument
		// expect nonce provider instead of WasabiRandom?
		public static (Knowledge knowledge, IEnumerable<GroupElement> bitCommitments) RangeProof(Scalar a, Scalar r, int width, WasabiRandom rnd)
		{
			var ma = a * Generators.Gg + r * Generators.Gh;
			var bits = Enumerable.Range(0, width).Select(i => a.GetBits(i, 1) == 0 ? Scalar.Zero : Scalar.One);

			// Generate bit commitments.
			// FIXME
			// - derive r_i from a, r, and additional randomness (like synthetic nonces)?
			// - maybe derive without randomness for idempotent requests?
			//   (deterministic blinding terms)? probably simpler to just save
			//   randomly generated credentials in memory or persistent storage, and
			//   re-request by loading and re-sending.
			// - long term fee credentials will definitely need deterministic
			//   randomness because the server can only give idempotent responses with
			//   its own records.
			var randomness = Enumerable.Repeat(0, width).Select(_ => rnd.GetScalar()).ToArray();
			var bitCommitments = bits.Zip(randomness, (b, r) => b * Generators.Gg + r * Generators.Gh);

			var columns = width * 3 + 1; // three witness terms per bit and one for the commitment
			int bitColumn(int i) => 3 * i + 1;
			int rndColumn(int i) => bitColumn(i) + 1;
			int productColumn(int i) => bitColumn(i) + 2;

			// Construct witness vector. First term is r from Ma = a*Gg + r*Gh. This
			// is followed by 3 witness terms per bit commitment, the bit b_i, the
			// randomness in its bit commitment r_i, and their product rb_i (0 or r).
			var witness = new Scalar[columns];
			witness[0] = r;
			foreach ((Scalar b_i, Scalar r_i, int i) in bits.Zip(randomness, Enumerable.Range(0, width), (x, y, z) => (x, y, z)))
			{
				witness[bitColumn(i)] = b_i;
				witness[rndColumn(i)] = r_i;
				witness[productColumn(i)] = r_i * b_i;
			}

			return (new Knowledge(RangeProof(ma, bitCommitments), new ScalarVector(witness)), bitCommitments);
		}

		// overload for bootstrap credential request proofs.
		// this is just a range proof with width=0
		// equivalent to new Statement(ma, Generators.Gh)
		public static Statement ZeroProof(GroupElement ma)
			=> RangeProof(ma, new GroupElement[0]);

		public static Statement RangeProof(GroupElement ma, IEnumerable<GroupElement> bitCommitments)
		{
			var width = bitCommitments.Count();
			Guard.True(nameof(width), width >= 0); // a width of 0 means no bits are set (commitment to 0)
			Guard.True(nameof(width), width <= 255); // 256 is tautological for the order of secp256k1

			var rows = width * 2 + 1; // two equations per bit, and one for the sum
			var columns = width * 3 + 1 + 1; // three witness components per bit and one for the Ma randomness, plus one for the public inputs

			// uninitialized values are implicitly O
			var equations = new GroupElement[rows, columns];

			// Proof of [ ( \sum 2^i * B_i ) - Ma = (\sum2^i r_i)*Gh - r*Gh ]
			// This means that the bit commitments, if they are bits (proven in
			// subsequent equations), are a decomposition of the amount committed in
			// Ma, proven by showing that the public input is a commitment to 0 (only
			// Gh term required to represent it). The per-bit witness terms of this
			// equation are added in the loop below.
			var bitsTotal = bitCommitments.Select((B, i) => Scalar.Zero.CAddBit((uint)i, 1) * B).Sum();
			equations[0, 0] = ma - bitsTotal;
			equations[0, 1] = Generators.Gh; // first witness term is r in Ma = a*Gg + r*Gh

			// Some helper functions to calculate indices of witness terms.
			// The witness structure is basically: r, zip3(b_i, r_i, rb_i)
			// So the terms in each equation are the public input (can be thought of
			// as a generator for a -1 term ) and the remaining are generators to
			// be used with 1+3n terms:
			//   ( r, b_0, r_0, rb_0, b_1, r_1, rb_1, ..., b_n, r_n, rb_n)
			int bitColumn(int i) => 3 * i + 2; // column for b_i witness term
			int rndColumn(int i) => bitColumn(i) + 1; // column for r_i witness term
			int productColumn(int i) => bitColumn(i) + 2; // column for rb_i witness term
			int bitRepresentationRow(int i) => 2 * i + 1; // row for B_i representation proof
			int bitSquaredRow(int i) => bitRepresentationRow(i) + 1; // row for [ b*(B_i-Gg) - rb*Gh <=> b = b*b ] proof

			// For each bit, add two equations and one term to the first equation.
			var B = bitCommitments.ToArray();
			for (int i = 0; i < bitCommitments.Count(); i++)
			{
				// Add [ -r_i * 2^i * Gh ] term to first equation.
				equations[0, rndColumn(i)] = Scalar.Zero.CAddBit((uint)i, 1) * Generators.Gh.Negate();

				// Add equation proving B is a Pedersen commitment to b:
				//   [ B = b*Gg + r*Gh ]
				equations[bitRepresentationRow(i), 0] = B[i];
				equations[bitRepresentationRow(i), bitColumn(i)] = Generators.Gg;
				equations[bitRepresentationRow(i), rndColumn(i)] = Generators.Gh;

				// Add an equation:
				//   [ O = b*(B - Gg) - rb*Gh ]
				// which proves that b is a bit:
				//   [ b = b*b  <=>  b \in {0,1} ]
				// assuming [ B = b*Gg + r*Gh ] as proven in the previous equation.
				//
				// This works because the following will be a commitment to 0 if and
				// only if b is a bit:
				//   [ b*(B-Gg) == b*((b*Gg)-Gg) + r*Gh == b*b*Gg - b*Gg + r*b*Gh =?= rb * Gh ]
				//
				// in the verification equation we require that the following terms
				// cancel out (public input point is O):
				equations[bitSquaredRow(i), bitColumn(i)] = B[i] - Generators.Gg;
				equations[bitSquaredRow(i), productColumn(i)] = Generators.Gh.Negate();
			}

			return new Statement(equations);
		}
	}
}
