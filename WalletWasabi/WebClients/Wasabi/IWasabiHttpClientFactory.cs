using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5.Pool.Circuits;

namespace WalletWasabi.WebClients.Wasabi;

public interface IWasabiHttpClientFactory
{
	(PersonCircuit, IHttpClient) NewHttpClientWithPersonCircuit()
	{
		PersonCircuit personCircuit = new();
		var httpClient = NewHttpClient(Mode.SingleCircuitPerLifetime, personCircuit);
		return (personCircuit, httpClient);
	}

	IHttpClient NewHttpClientWithDefaultCircuit()
	{
		return NewHttpClient(Mode.DefaultCircuit);
	}

	IHttpClient NewHttpClientWithCircuitPerRequest()
	{
		return NewHttpClient(Mode.NewCircuitPerRequest);
	}

	/// <remarks>This is a low-level method. Unless necessary, use a preceding convenience method.</remarks>
	IHttpClient NewHttpClient(Mode mode, ICircuit? circuit = null, int maximumRedirects = 0);
}
