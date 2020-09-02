using NBitcoin.Secp256k1;
using System.Linq;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge.LinearRelation
{
	// Each proof of a linear relation consists of multiple knowledge of
	// representation equations, all sharing a single witness comprised of several
	// secrets.
	//
	// Note that some of the generators can be the point at infinity, when a term
	// in the witness does not play a part in the representation of a specific
	// point.
	public class Equation
	{
		public Equation(GroupElement publicPoint, GroupElementVector generators)
		{
			CryptoGuard.NotInfinity(nameof(generators), generators);
			PublicPoint = CryptoGuard.NotInfinity(nameof(publicPoint), publicPoint);
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
			// the verification equation (for 1 generator case) is:
			//   sG =? R + eP
			// where:
			//   - R = kG is the public nonce, k is the secret nonce
			//   - P = xG is the public input, x is the secret
			//   - e is the challenge
			//   - s is the response
			return (publicNonce + challenge * PublicPoint) == responses * Generators;
		}

		// Simulate a public nonce given a challenge and arbitrary responses (should be random)
		internal GroupElement Simulate(Scalar challenge, ScalarVector givenResponses)
		{
			// The verification equation above can be rearranged as a formula for R
			// given e, P and s by subtracting eP from both sides:
			//   R = sG - eP
			return givenResponses * Generators - challenge * PublicPoint;
		}

		// Given a witness and secret nonces, respond to a challenge proving the equation holds w.r.t the witness
		internal ScalarVector Respond(ScalarVector witnesses, ScalarVector secretNonces, Scalar challenge)
		{
			// By canceling G on both sides of the verification equation above we can
			// obtain a formula for the response s given k, e and x:
			//   s = k + ex
			return secretNonces + challenge * witnesses;
		}
	}
}
