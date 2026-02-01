using System.Runtime.CompilerServices;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Secp256k1;
using NNostr.Client;

namespace WalletWasabi.Wallets.SilentPayment;

public static class NBitcoinExtensions
{
	public static SilentPaymentBech32Encoder GetSilentPaymentBech32Encoder(this Network network) =>
		new (Encoders.ASCII.DecodeData(GetHrpForNetwork(network)));

	private static string GetHrpForNetwork(Network network)
	{
		if (network == Network.Main)
		{
			return "sp";
		}
		if (network == Network.TestNet)
		{
			return "tsp";
		}
		if (network == Network.RegTest)
		{
			return "tprt";
		}

		throw new ArgumentException($"Network {network.Name} is not supported");
	}

	public static Key Tweak(this Key key)
	{
		using var eckey = ECPrivKey.Create(key.ToBytes());

		// Negate the key if the public key's y-coordinate is odd
		using var workingKey = eckey.CreatePubKey().Q.y.IsOdd
			? ECPrivKey.Create(eckey.sec.Negate().ToBytes(), eckey.ctx)
			: eckey;

		// Compute taproot tweak
		Span<byte> tweakHash = stackalloc byte[32];
		UnsafeTaprootFullPubKeyAccessor.ComputeTapTweak(null, key.PubKey.TaprootInternalKey, null, tweakHash);

		// Apply tweak and return new key
		using var tweakedKey = workingKey.TweakAdd(tweakHash);
		return new Key(tweakedKey.ToBytes());
	}
}

public class UnsafeTaprootFullPubKeyAccessor
{
      [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "ComputeTapTweak")]
      public static extern void ComputeTapTweak(TaprootFullPubKey self, TaprootInternalPubKey pk, uint256? merkleRoot, Span<byte> buf);
  }
