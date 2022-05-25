using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using Moq;
using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Models.Windows;
using WalletWasabi.Models;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5.Pool.Circuits;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

public class CoinJoinManagerTests
{
	[Fact]
	public async Task XTestAsync()
	{
		// Creates the dependencies: Wallets, RoundStateUpdater, HttpClientFactory and Best Height provider

		// List of wallets (typically owned by the wallet manager)
		var wallets = new List<IWallet>();

		// RoundStatusUpdater that returns a RoundState in InputRegistration phase
		var roundState = RoundState.FromRound(WabiSabiFactory.CreateRound(cfg: new()));
		using var roundStatusUpdater = BuildRoundStatusUpdater(roundState);
		await roundStatusUpdater.StartAsync(CancellationToken.None);

		// HttpClientFactory
		using PersonCircuit personCircuit = new();
		var httpClientFactory = BuildHttpClientFactory(personCircuit);

		// Best height provider (very-poor man implementation)
		int? bestHeight = null;
		void SetBestHeight(int? height) => bestHeight = height;
		int? GetBestHeight() => bestHeight;

		var serviceConfiguration = new ServiceConfiguration(new IPEndPoint(IPAddress.Loopback, 8080), Money.Zero);

		// Creates the CoinJoinManager instances and start it. (note that at this moment the wallet is not synchronized yet)
		using var cjm = new CoinJoinManager(wallets, roundStatusUpdater, httpClientFactory, serviceConfiguration, GetBestHeight);
		await cjm.StartAsync(CancellationToken.None);

		// creates a wallet with one coin and add it to the wallet list (this is exactly what happens when we open a new wallet)
		var keyManager = KeyManager.Recover(new Mnemonic("all all all all all all all all all all all all"), "", Network.Main, KeyManager.GetAccountKeyPath(Network.Main));
		var coin = BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(keyManager), Money.Coins(0.1m));
		var wallet = new TesteableWallet("My little wallet", keyManager, new []{ coin });
		wallets.Add(wallet);

		// The CoinJoinManager detects there is a new available wallet and raise the `LoadedEventArgs` event.
		var evnt = await cjm.WaitForEvent(TimeSpan.FromSeconds(1));
		Assert.True(evnt is LoadedEventArgs loaded && loaded.Wallet == wallet);

		// We instruct the CJM to start mixing the wallet.
		await cjm.StartAsync(wallet, stopWhenAllMixed: true, overridePlebStop: true, CancellationToken.None);

		// The wallet is not synchronized and that's why it communicates that with a `BackendNotSynchronized` error.
		// The CJM will wait for 30 seconds before trying again.
		evnt = await cjm.WaitForEvent(TimeSpan.FromSeconds(1));
		Assert.True(evnt is StartErrorEventArgs {Error: CoinjoinError.BackendNotSynchronized});

		// We simulate the wallet is finally synchronized (latest wasabiSynchronizer response has arrived)
		SetBestHeight(700_000);

		// After aprox 30 seconds the wallet will start coinjoining.
		evnt = await cjm.WaitForEvent(TimeSpan.FromSeconds(40));
		Assert.True(evnt is StartedEventArgs started /*&& started.RegistrationTimeout == roundState.InputRegistrationTimeout*/);

		await cjm.StopAsync(CancellationToken.None);
		await roundStatusUpdater.StopAsync(CancellationToken.None);
	}

	private static IWasabiHttpClientFactory BuildHttpClientFactory(PersonCircuit personCircuit)
	{
		IHttpClient httpClientWrapper = new HttpClientWrapper(new HttpClient());
		var mockHttpClientFactory = new Mock<IWasabiHttpClientFactory>(MockBehavior.Strict);

		mockHttpClientFactory
			.Setup(factory => factory.NewHttpClientWithPersonCircuit(out httpClientWrapper))
			.Returns(personCircuit);
		return mockHttpClientFactory.Object;
	}

	private static RoundStateUpdater BuildRoundStatusUpdater(RoundState roundState)
	{
		var mockApiClient = new Mock<IWabiSabiApiRequestHandler>();
		mockApiClient
			.Setup(apiClient => apiClient.GetStatusAsync(It.IsAny<RoundStateRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(
				() => new RoundStateResponse(
					new[] { roundState with {Phase = Phase.InputRegistration}},
					Array.Empty<CoinJoinFeeRateMedian>()));

		using RoundStateUpdater roundStatusUpdater = new(TimeSpan.FromSeconds(100), mockApiClient.Object);
		return roundStatusUpdater;
	}
}

public class TesteableWallet : IWallet
{
	private readonly IEnumerable<SmartCoin> _coins;
	public string WalletName { get; }
	public bool CanSpend { get; } = true;
	public KeyManager KeyManager { get; }
	public Kitchen Kitchen { get; }

	public TesteableWallet(string name, KeyManager keyManager, IEnumerable<SmartCoin> coins)
	{
		WalletName = name;
		KeyManager = keyManager;
		Kitchen = new Kitchen();
		_coins = coins;
	}

	public IEnumerator<SmartCoin> GetEnumerator()
	{
		return _coins.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}

public static class CoinJoinManagerExtensions
{
	public static async Task<StatusChangedEventArgs> WaitForEvent(this CoinJoinManager cjm, TimeSpan waitTime)
	{
		EventsAwaiter<StatusChangedEventArgs> statusChanged = new(
			h => cjm.StatusChanged += h,
			h => cjm.StatusChanged -= h, 1);

		var events = await statusChanged.WaitAsync(waitTime);
		return events.Single();
	}

}