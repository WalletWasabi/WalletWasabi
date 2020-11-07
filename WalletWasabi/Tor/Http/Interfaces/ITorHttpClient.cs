using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Tor.Http.Interfaces
{
	public interface ITorHttpClient : IHttpClient
	{
		bool IsTorUsed { get; }

		Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUri, HttpContent? content = null, CancellationToken cancel = default);
	}
}
