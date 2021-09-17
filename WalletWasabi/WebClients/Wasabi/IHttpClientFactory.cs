using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5.Pool.Circuits;

namespace WalletWasabi.WebClients.Wasabi
{
	public interface IBackendHttpClientFactory
	{
		IHttpClient NewBackendHttpClient(Mode mode);
	}
}
