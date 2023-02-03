namespace WalletWasabi.Tor.Socks5.Pool;

public record TorPrebuildCircuitRequest
{
	public TorPrebuildCircuitRequest(Uri baseUri, TimeSpan randomDelay)
	{
		BaseUri = baseUri;
		RandomDelay = randomDelay;
	}

	public Uri BaseUri { get; }
	public TimeSpan RandomDelay { get; }
}
