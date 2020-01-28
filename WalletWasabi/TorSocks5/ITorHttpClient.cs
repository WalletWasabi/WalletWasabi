using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.TorSocks5
{
	public interface ITorHttpClient : IDisposable
	{
		Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUri, HttpContent content = null, CancellationToken cancel = default);

		Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel = default);

		Uri DestinationUri { get; }
		Func<Uri> DestinationUriAction { get; }
		EndPoint TorSocks5EndPoint { get; }

		bool IsTorUsed { get; }
	}
}
