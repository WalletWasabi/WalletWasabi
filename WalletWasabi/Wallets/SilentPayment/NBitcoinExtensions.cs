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

		var eckey = ECPrivKey.Create(key.ToBytes());

		if (eckey.CreatePubKey().Q.y.IsOdd)
		{
			eckey = ECPrivKey.Create(eckey.sec.Negate().ToBytes(), eckey.ctx);
		}
		Span<byte> buf = stackalloc byte[32];
		UnsafeTaprootFullPubKeyAccessor.ComputeTapTweak(null, key.PubKey.TaprootInternalKey, null, buf);
		eckey = eckey.TweakAdd(buf);

		return new Key(eckey.ToBytes());
	}
}

public class UnsafeTaprootFullPubKeyAccessor
{
      [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "ComputeTapTweak")]
      public static extern void ComputeTapTweak(TaprootFullPubKey self, TaprootInternalPubKey pk, uint256? merkleRoot, Span<byte> buf);
  }
