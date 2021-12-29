using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5.Pool.Circuits;

namespace WalletWasabi.WebClients.Wasabi
{
	public interface IWasabiHttpClientFactory
	{
		IHttpClient NewBackendHttpClient(Mode mode, ICircuit? circuit = null);
	}
}
