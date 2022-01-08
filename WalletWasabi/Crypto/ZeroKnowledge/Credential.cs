using NBitcoin.Secp256k1;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.ZeroKnowledge;

/// <summary>
/// Represents an anonymous credential and its represented data.
/// </summary>
public record Credential
{
	/// <summary>
	/// Initializes a new Credential instance.
	/// </summary>
	/// <param name="value">The amount represented by the credential.</param>
	/// <param name="randomness">The randomness used as blinding factor in the Pedersen committed amount.</param>
	/// <param name="mac">The algebraic MAC representing the anonymous credential issued by the coordinator.</param>
	public Credential(long value, Scalar randomness, MAC mac)
	{
		Value = Guard.MinimumAndNotNull(nameof(value), value, 0);
		Randomness = randomness;
		Mac = mac;
	}

	/// <summary>
	/// Amount represented by the credential.
	/// </summary>
	public long Value { get; }

	/// <summary>
	/// Randomness used as blinding factor for the Pedersen committed Amount.
	/// </summary>
	public Scalar Randomness { get; }

	/// <summary>
	/// Algebraic MAC representing the anonymous credential issued by the coordinator.
	/// </summary>
	public MAC Mac { get; }

	/// <summary>
	/// Randomizes the credential using a randomization scalar z.
	/// </summary>
	/// <param name="z">The randomization scalar.</param>
	/// <returns>A randomized credential ready to be presented to the coordinator.</returns>
	internal CredentialPresentation Present(Scalar z)
	{
		GroupElement Randomize(GroupElement g, GroupElement m) => m + z * g;
		return new CredentialPresentation(
			ca: Randomize(Generators.Ga, ProofSystem.PedersenCommitment(new Scalar((ulong)Value), Randomness)),
			cx0: Randomize(Generators.Gx0, Mac.U),
			cx1: Randomize(Generators.Gx1, Mac.T * Mac.U),
			cV: Randomize(Generators.GV, Mac.V),
			s: Randomness * Generators.Gs);
	}
}
