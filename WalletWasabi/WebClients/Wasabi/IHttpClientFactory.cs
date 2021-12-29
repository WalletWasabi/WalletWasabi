using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5.Pool.Circuits;

namespace WalletWasabi.WebClients.Wasabi
{
	public interface IWasabiHttpClientFactory
	{
		IHttpClient NewHttpClient(Mode mode, ICircuit? circuit = null);
	}
}
