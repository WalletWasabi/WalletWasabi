using NBitcoin.Secp256k1;
using WalletWasabi.Crypto.Groups;

namespace WalletWasabi.Crypto.ZeroKnowledge
{
	/// <summary>
	/// Represents an anonymous credential and its represented data.
	/// </summary>
	public class Credential
	{
		/// <summary>
		/// Initializes a new Credential instance.
		/// </summary>
		/// <param name="amount">The amount represented by the credential.</param>
		/// <param name="randomness">The randomness used as blinding factor in the pedersen committed amount.</param>
		/// <param name="mac">The algebraic MAC represented the anonymous credential issued by the coordinator.</param>
		public Credential(Scalar amount, Scalar randomness, MAC mac)
		{
			Amount = amount;
			Randomness = randomness;
			Mac = mac;
		}

		/// <summary>
		/// Amount in represented by the credential.
		/// </summary>
		public Scalar Amount { get; }

		/// <summary>
		/// Randomness used as blinding factor for the pedersen committed Amount.
		/// </summary>
		public Scalar Randomness { get; }

		/// <summary>
		/// Algebraic MAC represented the anonymous credential issued by the coordinator.
		/// </summary>
		public MAC Mac { get; }

		/// <summary>
		/// Randomizes the credential using a randomization scalar z.
		/// </summary>
		/// <param name="z">The randomization scalar.</param>
		/// <returns>A randomized credential ready to be presented to the coordinator.</returns>
		internal CredentialPresentation Present(Scalar z)
		{
			GroupElement Randomize(GroupElement G, GroupElement M) => M + z * G;
			return new CredentialPresentation(
				Ca: Randomize(Generators.Ga, Amount * Generators.Gg + Randomness * Generators.Gh),
				Cx0: Randomize(Generators.Gx0, Mac.U),
				Cx1: Randomize(Generators.Gx1, Mac.T * Mac.U),
				CV: Randomize(Generators.GV, Mac.V),
				S: Randomness * Generators.Gs);
		}
	}
}
