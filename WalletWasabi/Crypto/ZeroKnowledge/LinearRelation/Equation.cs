using NBitcoin.Secp256k1;
using System.Linq;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge.LinearRelation
{
	// Each proof of a linear relation consists of multiple knowledge of
	// representation equations, all sharing a single witness comprised of several
	// secret scalars.
	//
	// Knowledge of representation means that given a curve point P, the prover
	// knows how to construct P from a set of predetermined generators. This is
	// commonly referred to as multi-exponentiation but that term implies
	// multiplicative notation for the group operation. Written additively and
	// indexing by `i`, each equation is of the form:
	//
	//     P_i = x_1 * G_{i,1} + x_2 * G_{i,2} + ... + x_n * G_{i,n}
	//
	// Note that some of the generators for an equation can be the point at
	// infinity when a term in the witness does not play a part in the
	// representation of a specific point.
	public class Equation
	{
		public Equation(GroupElement publicPoint, GroupElementVector generators)
		{
			Guard.NotNullOrEmpty(nameof(generators), generators);
			PublicPoint = Guard.NotNull(nameof(publicPoint), publicPoint);
			Generators = generators;
		}

		// Knowledge of representation asserts
		//     P = x_1*G_1 + x_2*G_2 + ...
		// so we need a single public input and several generators
		public GroupElement PublicPoint { get; }

		public GroupElementVector Generators { get; }

		// Evaluate the verification equation corresponding to the one in the statement
		internal bool Verify(GroupElement publicNonce, Scalar challenge, ScalarVector responses)
		{
			// A challenge of 0 does not place any constraint on the witness
			if (challenge == Scalar.Zero)
			{
				return false;
			}

			// the verification equation (for 1 generator case) is:
			//   sG =? R + eP
			// where:
			//   - R = kG is the public nonce, k is the secret nonce
			//   - P = xG is the public input, x is the secret
			//   - e is the challenge
			//   - s is the response
			return responses * Generators == (publicNonce + challenge * PublicPoint);
		}

		// Given a witness and secret nonces, respond to a challenge proving the equation holds w.r.t the witness
		internal static ScalarVector Respond(ScalarVector witness, ScalarVector secretNonces, Scalar challenge)
		{
			// By canceling G on both sides of the verification equation above we can
			// obtain a formula for the response s given k, e and x:
			//   s = k + ex
			return secretNonces + challenge * witness;
		}

		internal bool VerifySolution(ScalarVector witness)
		{
			return PublicPoint == witness * Generators;
		}
	}
}
