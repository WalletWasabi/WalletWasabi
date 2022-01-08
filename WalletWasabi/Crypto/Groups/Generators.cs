using NBitcoin.Secp256k1;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace WalletWasabi.Crypto.Groups;

public static class Generators
{
	/// <summary>
	/// Base point defined in the secp256k1 standard used in ECDSA public key derivation.
	/// </summary>
	public static GroupElement G { get; } = new GroupElement(EC.G);

	/// <summary>
	/// Generator point for MAC and Show.
	/// </summary>
	public static GroupElement Gw { get; } = FromText("Gw");

	/// <summary>
	/// Generator point for MAC and Show.
	/// </summary>
	public static GroupElement Gwp { get; } = FromText("Gwp");

	/// <summary>
	/// Generator point for MAC and Show.
	/// </summary>
	public static GroupElement Gx0 { get; } = FromText("Gx0");

	/// <summary>
	/// Generator point for MAC and Show.
	/// </summary>
	public static GroupElement Gx1 { get; } = FromText("Gx1");

	/// <summary>
	/// Generator point for MAC and Show.
	/// </summary>
	public static GroupElement GV { get; } = FromText("GV");

	/// <summary>
	/// Generator point for Pedersen commitments.
	/// </summary>
	public static GroupElement Gg { get; } = FromText("Gg");

	/// <summary>
	/// Generator point for Pedersen commitments.
	/// </summary>
	public static GroupElement Gh { get; } = FromText("Gh");

	/// <summary>
	/// Generator point for attributes M_{ai}.
	/// </summary>
	public static GroupElement Ga { get; } = FromText("Ga");

	/// <summary>
	/// Generator point for serial numbers.
	/// </summary>
	public static GroupElement Gs { get; } = FromText("Gs");

	/// <summary>
	/// Scalars corresponding to 2^i, used in range proofs.
	/// </summary>
	public static Scalar[] ScalarPowersOfTwo { get; } = Enumerable.Range(0, 255).Select(i => Scalar.Zero.CAddBit((uint)i, 1)).ToArray();

	/// <summary>
	/// Generators corresponding to -(2^i) * Gh, used in range proofs.
	/// </summary>
	public static GroupElement[] NegatedGhPowersOfTwo { get; } = ScalarPowersOfTwo.Select(b => b.Negate() * Gh).ToArray();

	public static bool TryGetFriendlyGeneratorName(GroupElement? ge, [NotNullWhen(true)] out string? name)
	{
		static string FormatName(string generatorName) => $"{generatorName} Generator";
		name = ge switch
		{
			_ when ge == G => FormatName("Standard"),
			_ when ge == Gw => FormatName(nameof(Gw)),
			_ when ge == Gwp => FormatName(nameof(Gwp)),
			_ when ge == Gx0 => FormatName(nameof(Gx0)),
			_ when ge == Gx1 => FormatName(nameof(Gx1)),
			_ when ge == GV => FormatName(nameof(GV)),
			_ when ge == Gg => FormatName(nameof(Gg)),
			_ when ge == Gh => FormatName(nameof(Gh)),
			_ when ge == Ga => FormatName(nameof(Ga)),
			_ when ge == Gs => FormatName(nameof(Gs)),
			_ => ""
		};
		return name.Length != 0;
	}

	/// <summary>
	/// Deterministically creates a group element from the given text.
	/// Uniqueness relies on the SHA256 hash function.
	/// </summary>
	public static GroupElement FromText(string text)
		=> FromBuffer(Encoding.UTF8.GetBytes(text));

	/// <summary>
	/// Deterministically creates a group element from the given text.
	/// Uniqueness relies on the SHA256 hash function.
	/// </summary>
	public static GroupElement FromBuffer(byte[] buffer)
	{
		GE ge;
		using var sha256 = System.Security.Cryptography.SHA256.Create();
		do
		{
			buffer = sha256.ComputeHash(buffer);
		}
		while (!GE.TryCreateXQuad(new FE(buffer), out ge));

		return new GroupElement(ge);
	}
}
