using System.Linq;
using System.Collections.Generic;
using NBitcoin.Secp256k1;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge.LinearRelation;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge;

internal partial class ProofSystem
{
	private static GroupElement O = GroupElement.Infinity;

	private delegate Proof DeferredProofCreator(Scalar challenge);

	public static IEnumerable<Proof> Prove(Transcript transcript, IEnumerable<Knowledge> knowledge, WasabiRandom random)
	{
		// Before anything else all components in a compound proof commit to the
		// individual sub-statement that will be proven, ensuring that the
		// challenges and therefore the responses depend on the statement as a
		// whole.
		foreach (var k in knowledge)
		{
			transcript.CommitStatement(k.Statement);
		}

		var deferredResponds = new List<DeferredProofCreator>();
		foreach (var k in knowledge)
		{
			// With all the statements committed, generate a vector of random secret
			// nonces for every equation in underlying proof system. In order to
			// ensure that nonces are never reused (e.g. due to an insecure RNG) with
			// different challenges which would leak the witness, these are generated
			// as synthetic nonces that also depend on the witness data.
			var secretNonceProvider = transcript.CreateSyntheticSecretNonceProvider(k.Witness, random);
			ScalarVector secretNonces = secretNonceProvider.GetScalarVector();

			// The prover then commits to these, adding the corresponding public
			// points to the transcript.
			var equations = k.Statement.Equations;
			var publicNonces = new GroupElementVector(equations.Select(equation => secretNonces * equation.Generators));
			transcript.CommitPublicNonces(publicNonces);

			deferredResponds.Add((challenge) => new Proof(publicNonces, k.RespondToChallenge(challenge, secretNonces)));
		}

		// With the public nonces committed to the transcript the prover can then
		// derive a challenge that depend on the transcript state without needing
		// to interact with the verifier, but ensuring that they can't know the
		// challenge before the prover commitments are generated.
		Scalar challenge = transcript.GenerateChallenge();
		return deferredResponds.Select(createProof => createProof(challenge));
	}

	public static bool Verify(Transcript transcript, IEnumerable<Statement> statements, IEnumerable<Proof> proofs)
	{
		Guard.Same(nameof(proofs), proofs.Count(), statements.Count());

		// Before anything else all components in a compound proof commit to the
		// individual sub-statement that will be proven, ensuring that the
		// challenges and therefore the responses depend on the statement as a
		// whole.
		foreach (var statement in statements)
		{
			transcript.CommitStatement(statement);
		}

		// After all the statements have been committed, the public nonces are
		// added to the transcript. This is done separately from the statement
		// commitments because the prover derives these based on the compound
		// statements, and the verifier must add data to the transcript in the
		// same order as the prover.
		foreach (var proof in proofs)
		{
			transcript.CommitPublicNonces(proof.PublicNonces);
		}

		// After all the public nonces have been committed, a challenge can be
		// generated based on transcript state. Since challenges are deterministic
		// outputs of a hash function which depends on the prover commitments, the
		// verifier obtains the same challenge and then accepts if the responses
		// satisfy the verification equation.
		var challenge = transcript.GenerateChallenge();

		return Enumerable.Zip(statements, proofs, (s, p) => s.CheckVerificationEquation(p.PublicNonces, challenge, p.Responses)).All(x => x);
	}

	public static Knowledge IssuerParametersKnowledge(MAC mac, GroupElement ma, CredentialIssuerSecretKey sk)
		=> new(IssuerParametersStatement(sk.ComputeCredentialIssuerParameters(), mac, ma), new ScalarVector(sk.W, sk.Wp, sk.X0, sk.X1, sk.Ya));

	public static Statement IssuerParametersStatement(CredentialIssuerParameters iparams, MAC mac, GroupElement ma)
		=> new(new GroupElement[,]
		{
				// public                                             Witness terms:
				// point                     w,             wp,             x0,             x1,             ya
				{ mac.V,                     Generators.Gw, O,              mac.U,          mac.T * mac.U,  ma },
				{ Generators.GV - iparams.I, O,             O,              Generators.Gx0, Generators.Gx1, Generators.Ga },
				{ iparams.Cw,                Generators.Gw, Generators.Gwp, O,              O,              O },
		});

	public static Knowledge ShowCredentialKnowledge(CredentialPresentation presentation, Scalar z, Credential credential, CredentialIssuerParameters iparams)
		=> new(
			ShowCredentialStatement(presentation, z * iparams.I, iparams),
			new ScalarVector(z, (credential.Mac.T * z).Negate(), credential.Mac.T, new Scalar((ulong)credential.Value), credential.Randomness));

	public static Statement ShowCredentialStatement(CredentialPresentation c, GroupElement z, CredentialIssuerParameters iparams)
		=> new(new GroupElement[,]
		{
				// public                     Witness terms:
				// point      z               z0              t               a               r
				{ z,          iparams.I,      O,              O,              O,              O },
				{ c.Cx1,      Generators.Gx1, Generators.Gx0, c.Cx0,          O,              O },
				{ c.Ca,       Generators.Ga,  O,              O,              Generators.Gg,  Generators.Gh },
				{ c.S,        O,              O,              O,              O,              Generators.Gs }
		});

	public static Knowledge BalanceProofKnowledge(Scalar zSum, Scalar rDeltaSum)
		=> new(BalanceProofStatement(zSum * Generators.Ga + rDeltaSum * Generators.Gh), new ScalarVector(zSum, rDeltaSum));

	// Balance commitment must be a commitment to 0, with randomness in Gh and
	// additional randomness from attribute randomization in Show protocol,
	// using generator Ga. Witness terms: (\sum z, \sum r_i - r'_i)
	public static Statement BalanceProofStatement(GroupElement balanceCommitment)
		=> new(balanceCommitment, Generators.Ga, Generators.Gh);

	// overload for bootstrap credential request proofs.
	// this is just a range proof with width=0
	// equivalent to proof of representation w/ Gh
	public static Knowledge ZeroProofKnowledge(GroupElement ma, Scalar r)
		=> new(ZeroProofStatement(ma), new ScalarVector(r));

	public static GroupElement PedersenCommitment(Scalar s, Scalar b)
	{
		var gej = ECMultContext.Instance.MultBatch(
								   new[] { s, b },
								   new[] { Generators.Gg.Ge, Generators.Gh.Ge });
		return new GroupElement(gej);
	}

	// TODO swap return value order, remove GroupElement argument
	// expect nonce provider instead of WasabiRandom?
	public static (Knowledge knowledge, IEnumerable<GroupElement> bitCommitments) RangeProofKnowledge(Scalar a, Scalar r, int width, WasabiRandom rnd)
	{
		var ma = PedersenCommitment(a, r);
		var bits = Enumerable.Range(0, width).Select(i => a.GetBits(i, 1) == 0 ? Scalar.Zero : Scalar.One);

		// Generate bit commitments.
		// FIXME
		// - derive r_i from a, r, and additional randomness (like synthetic nonces)?
		// - maybe derive without randomness for idempotent requests?
		//   (deterministic blinding terms)? probably simpler to just save
		//   randomly generated credentials in memory or persistent storage, and
		//   re-request by loading and re-sending.
		// - long term fee credentials will definitely need deterministic
		//   randomness because otherwise recovery of credentials from
		//   seed would not result in idempotent responses if the client
		//   loses state
		var randomness = Enumerable.Repeat(0, width).Select(_ => rnd.GetScalar()).ToArray();
		var bitCommitments = bits.Zip(randomness, (b, r) => PedersenCommitment(b, r)).ToArray();

		var columns = width * 3 + 1; // three witness terms per bit and one for the commitment
		static int BitColumn(int i) => 3 * i + 1;
		static int RndColumn(int i) => BitColumn(i) + 1;
		static int ProductColumn(int i) => BitColumn(i) + 2;

		// Construct witness vector. First term is r from Ma = a*Gg + r*Gh. This
		// is followed by 3 witness terms per bit commitment, the bit b_i, the
		// randomness in its bit commitment r_i, and their product rb_i (0 or r).
		var witness = new Scalar[columns];
		witness[0] = r;
		foreach ((Scalar b_i, Scalar r_i, int i) in bits.Zip(randomness, Enumerable.Range(0, width), (x, y, z) => (x, y, z)))
		{
			witness[BitColumn(i)] = b_i;
			witness[RndColumn(i)] = r_i;
			witness[ProductColumn(i)] = r_i * b_i;
		}

		return (new Knowledge(RangeProofStatement(ma, bitCommitments, width), new ScalarVector(witness)), bitCommitments);
	}

	// overload for bootstrap credential request proofs.
	// this is just a range proof with width=0
	// equivalent to new Statement(ma, Generators.Gh)
	public static Statement ZeroProofStatement(GroupElement ma)
		=> RangeProofStatement(ma, Array.Empty<GroupElement>(), 0);

	public static Statement RangeProofStatement(GroupElement ma, IEnumerable<GroupElement> bitCommitments, int width)
	{
		Guard.InRangeAndNotNull(nameof(width), width, 0, 255);
		var b = bitCommitments.ToArray();
		Guard.Equals(b.Length, width);

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
		var powersOfTwo = Generators.ScalarPowersOfTwo.AsSpan(0, width);
		var bitsTotal = new GroupElement(ECMultContext.Instance.MultBatch(powersOfTwo, b.Select(x => x.Ge).ToArray()));

		equations[0, 0] = ma - bitsTotal;
		equations[0, 1] = Generators.Gh; // first witness term is r in Ma = a*Gg + r*Gh

		// Some helper functions to calculate indices of witness terms.
		// The witness structure is basically: r, zip3(b_i, r_i, rb_i)
		// So the terms in each equation are the public input (can be thought of
		// as a generator for a -1 term ) and the remaining are generators to
		// be used with 1+3n terms:
		//   ( r, b_0, r_0, rb_0, b_1, r_1, rb_1, ..., b_n, r_n, rb_n)
		static int BitColumn(int i) => 3 * i + 2; // column for b_i witness term
		static int RndColumn(int i) => BitColumn(i) + 1; // column for r_i witness term
		static int ProductColumn(int i) => BitColumn(i) + 2; // column for rb_i witness term
		static int BitRepresentationRow(int i) => 2 * i + 1; // row for B_i representation proof
		static int BitSquaredRow(int i) => BitRepresentationRow(i) + 1; // row for [ b*(B_i-Gg) - rb*Gh <=> b = b*b ] proof

		// For each bit, add two equations and one term to the first equation.
		var negatedGh = Generators.Gh.Negate();
		for (int i = 0; i < width; i++)
		{
			// Add [ -r_i * 2^i * Gh ] term to first equation.
			equations[0, RndColumn(i)] = Generators.NegatedGhPowersOfTwo[i];

			// Add equation proving B is a Pedersen commitment to b:
			//   [ B = b*Gg + r*Gh ]
			equations[BitRepresentationRow(i), 0] = b[i];
			equations[BitRepresentationRow(i), BitColumn(i)] = Generators.Gg;
			equations[BitRepresentationRow(i), RndColumn(i)] = Generators.Gh;

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
			equations[BitSquaredRow(i), BitColumn(i)] = b[i] - Generators.Gg;
			equations[BitSquaredRow(i), ProductColumn(i)] = negatedGh;
		}

		return new Statement(equations);
	}
}
