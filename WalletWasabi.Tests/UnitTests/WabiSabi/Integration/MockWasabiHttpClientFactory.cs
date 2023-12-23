using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5.Pool.Circuits;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Integration;

public class MockWasabiHttpClientFactory : IWasabiHttpClientFactory
{
	public Func<(PersonCircuit, IHttpClient)>? OnNewHttpClientWithPersonCircuit { get; set; }
	public Func<IHttpClient>? OnNewHttpClientWithCircuitPerRequest { get; set; }
	public Func<IHttpClient>? OnNewHttpClientWithDefaultCircuit { get; set; }

	public (PersonCircuit, IHttpClient) NewHttpClientWithPersonCircuit() =>
		OnNewHttpClientWithPersonCircuit?.Invoke()
			?? throw new NotImplementedException($"{nameof(NewHttpClientWithPersonCircuit)} was called but never assigned.");

	public IHttpClient NewHttpClientWithCircuitPerRequest() =>
		OnNewHttpClientWithCircuitPerRequest?.Invoke()
			?? throw new NotImplementedException($"{nameof(NewHttpClientWithPersonCircuit)} was called but never assigned.");

	public IHttpClient NewHttpClientWithDefaultCircuit() =>
		OnNewHttpClientWithDefaultCircuit?.Invoke()
			?? throw new NotImplementedException($"{nameof(NewHttpClientWithDefaultCircuit)} was called but never assigned.");

	public IHttpClient NewHttpClient(Mode mode, ICircuit? circuit = null, int maximumRedirects = 0) =>
		throw new NotImplementedException();
}
