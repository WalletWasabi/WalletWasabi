using NBitcoin;
using NBitcoin.DataEncoders;

namespace WalletWasabi.Wallets.SilentPayment;

public static class NBitcoinExtensions
{
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

	public static SilentPaymentBech32Encoder GetSilentPaymentBech32Encoder(this Network network) =>
		new (Encoders.ASCII.DecodeData(GetHrpForNetwork(network)));
}
