using System.Linq;
using NBitcoin.Secp256k1;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Crypto.ZeroKnowledge.LinearRelation;

namespace WalletWasabi.Tests.UnitTests.Crypto;

internal class ProofSystemHelpers
{
	public static bool Verify(Statement statement, Proof proof)
	{
		return ProofSystem.Verify(new Transcript(Array.Empty<byte>()), new[] { statement }, new[] { proof });
	}

	public static Proof Prove(Knowledge knowledge, WasabiRandom random)
	{
		return ProofSystem.Prove(new Transcript(Array.Empty<byte>()), new[] { knowledge }, random).First();
	}

	public static Proof Prove(Statement statement, Scalar witness, WasabiRandom random)
	{
		return Prove(statement, new ScalarVector(witness), random);
	}

	public static Proof Prove(Statement statement, ScalarVector witness, WasabiRandom random)
	{
		return Prove(new Knowledge(statement, witness), random);
	}
}
