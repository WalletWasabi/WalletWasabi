
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.TorSocks5.Socks
{
	public class SocksHttpClientHandler : HttpClientHandler
	{
		public SocksHttpClientHandler(ITorHttpClient client)
		{
			Client = client;
		}

		public ITorHttpClient Client { get; }

		public override bool Equals(object obj)
		{
			return base.Equals(obj);
		}

		public override int GetHashCode()
		{
			return base.GetHashCode();
		}

		public override string ToString()
		{
			return base.ToString();
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
		}

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			return Client.SendAsync(request, cancellationToken);
		}
	}
}

