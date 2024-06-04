using NBitcoin;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;

namespace WalletWasabi.Nostr;

public record NostrCoordinator(string Description, string Name, Uri Uri, Network Network)
{
	private static readonly NostrCoordinator Current = new(
		Description: "WabiSabi Coinjoin Coordinator",
		Name: "WabiSabi Coordinator",
		Uri: new Uri(Constants.BackendUri),
		Network: Network.Main);

	public static NostrCoordinator GetCoordinator(Network network)
	{
		if (network == Network.Main)
		{
			return Current;
		}

		if (network == Network.TestNet)
		{
			return Current with {Uri = new Uri(Constants.TestnetBackendUri), Network = Network.TestNet};
		}

		if (network == Network.RegTest)
		{
			return Current with {Uri = new Uri("http://localhost:37127/"), Network = Network.RegTest};
		}

		throw new NotSupportedNetworkException(network);
	}
};
