using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5.Pool.Circuits;

namespace WalletWasabi.WebClients.Wasabi;

public interface IWasabiHttpClientFactory
{
	PersonCircuit NewHttpClientWithPersonCircuit(out IHttpClient httpClient)
	{
		PersonCircuit personCircuit = new();
		httpClient = NewHttpClient(Mode.SingleCircuitPerLifetime, personCircuit);
		return personCircuit;
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
	IHttpClient NewHttpClient(Mode mode, ICircuit? circuit = null);
}
