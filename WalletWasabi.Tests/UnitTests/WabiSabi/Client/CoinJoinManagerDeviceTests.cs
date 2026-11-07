using NBitcoin;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Crypto;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.UnitTests.Hwi;
using WalletWasabi.Tests.UnitTests.Services;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.Banning;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin.Manager;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
using WalletWasabi.WabiSabi.Coordinator.PostRequests;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

/// <summary>How the coinjoin manager treats a wallet whose device has to authorize its rounds, against a device that answers what the test says.</summary>
public class CoinJoinManagerDeviceTests
{
	/// <summary>Taking the device away while it shows the prompt would lose the authorization, so the operations that take it have to see the wallet is busy.</summary>
	[Fact]
	public async Task AWalletIsBusyWhileItsDeviceAsksAsync()
	{
		using var device = new FakeDevice();
		await using var context = await ManagerContext.StartAsync(device);

		context.Start();
		await UntilAsync(() => device.Asked == 1);
		await UntilAsync(() => context.ClientState == CoinJoinClientState.InSchedule);

		device.Answer.SetException(new HardwareWalletException("The user declined."));
		await UntilAsync(() => context.HasError(CoinjoinError.DeviceAuthorizationFailed));
		await UntilAsync(() => context.ClientState == CoinJoinClientState.Idle);
	}

	/// <summary>
	/// A device lights up only for a start the user asked for. So a round that ends with the authorization gone stops the
	/// wallet instead of restarting into a prompt nobody is waiting for, while a round that keeps it restarts as for any wallet.
	/// </summary>
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task OnlyARoundThatLostTheAuthorizationStopsTheWalletAsync(bool authorizationLost)
	{
		using var device = new FakeDevice();
		await using var context = await ManagerContext.StartAsync(device);

		// The round has nothing to mix by the time it starts, so it ends right away.
		device.WhenAsked = () => context.Coin.IsExcludedFromCoinJoin = true;
		device.Answer.SetResult(new RoundLosingKeyChain(authorizationLost));

		context.Start();

		if (authorizationLost)
		{
			await UntilAsync(() => context.HasError(CoinjoinError.DeviceAuthorizationFailed));
			Assert.Contains(context.Events, e => e is WalletStoppedCoinJoinEventArgs);
			await UntilAsync(() => context.ClientState == CoinJoinClientState.Idle);
		}
		else
		{
			await UntilAsync(() => context.ClientState == CoinJoinClientState.InSchedule && context.Events.Any(e => e is CompletedEventArgs));
			Assert.False(context.HasError(CoinjoinError.DeviceAuthorizationFailed));
		}

		Assert.Equal(1, device.Asked);
	}

	private static async Task UntilAsync(Func<bool> condition)
	{
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
		while (!condition())
		{
			await Task.Delay(50, timeout.Token);
		}
	}

	private sealed class ManagerContext : IAsyncDisposable
	{
		private readonly CoinJoinManager _manager;
		private readonly CoinPrison _coinPrison;
		private readonly IDisposable _roundStateUpdater;

		private ManagerContext(CoinJoinManager manager, CoinPrison coinPrison, WalletWasabi.Wallets.Wallet wallet, SmartCoin coin, IDisposable roundStateUpdater)
		{
			_manager = manager;
			_coinPrison = coinPrison;
			Wallet = wallet;
			Coin = coin;
			_roundStateUpdater = roundStateUpdater;
			_manager.StatusChanged += (_, e) => Events.Enqueue(e);
		}

		public WalletWasabi.Wallets.Wallet Wallet { get; }
		public SmartCoin Coin { get; }
		public ConcurrentQueue<StatusChangedEventArgs> Events { get; } = new();

		/// <summary>The state the manager reports, once it has looked at its wallets for the first time.</summary>
		public CoinJoinClientState? ClientState
		{
			get
			{
				try
				{
					return _manager.GetCoinjoinClientState(Wallet.WalletId);
				}
				catch (ArgumentException)
				{
					return null;
				}
			}
		}

		public bool HasError(CoinjoinError error) => Events.OfType<StartErrorEventArgs>().Any(e => e.Error == error);

		public void Start() => _manager.RequestCoinJoinStart(Wallet, Wallet, stopWhenAllMixed: false, overridePlebStop: false);

		public static async Task<ManagerContext> StartAsync(FakeDevice device)
		{
			var eventBus = new EventBus();
			var wallet = WalletWasabi.Wallets.Wallet.CreateFactory(
				Network.Main,
				filterManager: null!,
				transactionStore: null!,
				filterHeaderChain: null!,
				mempoolService: null!,
				new ServiceConfiguration(Money.Coins(Constants.DefaultDustThreshold)),
				blockProvider: null!,
				eventBus,
				cpfpInfoProvider: null!,
				device)(TestKeyManagers.WatchOnlyHardwareWallet(withCoinJoinAccount: true));

			var coin = BitcoinFactory.CreateSmartCoin(wallet.KeyManager.GetNextReceiveKey(new LabelsArray("test"), ScriptPubKeyType.TaprootBIP86), Money.Coins(1m));
			Assert.True(wallet.Coins.TryAdd(coin));

			var requestHandler = new NoRoundsRequestHandler();
			var roundStateUpdater = RoundStateUpdaterForTesting.CreateManual(requestHandler, TestContext.Current.CancellationToken);
#pragma warning disable CA2000 // Dispose objects before losing scope - owned by the returned context.
			var coinPrison = CoinPrison.CreateOrLoadFromFile(await Common.GetEmptyWorkDirAsync());
#pragma warning restore CA2000
			var manager = new CoinJoinManager(
				() => Task.FromResult<IEnumerable<WalletWasabi.Wallets.Wallet>>([wallet]),
				new RoundStateProvider(roundStateUpdater),
				_ => requestHandler,
				new CoinJoinConfiguration("coordinator", 150m, AbsoluteMinInputCount: 2, AllowSoloCoinjoining: false),
				coinPrison,
				InputVerifiers.NoVerification(),
				eventBus);
			await manager.StartAsync(TestContext.Current.CancellationToken);

			return new ManagerContext(manager, coinPrison, wallet, coin, roundStateUpdater);
		}

		public async ValueTask DisposeAsync()
		{
			await _manager.StopAsync(CancellationToken.None);
			_manager.Dispose();
			_coinPrison.Dispose();
			_roundStateUpdater.Dispose();
			Wallet.Dispose();
		}
	}

	private sealed class FakeDevice : HardwareWalletService
	{
		private int _asked;

		public FakeDevice() : base(Network.Main)
		{
		}

		public int Asked => _asked;
		public TaskCompletionSource<IKeyChain> Answer { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public Action? WhenAsked { get; set; }

		public override Task<IKeyChain> AuthorizeCoinJoinAsync(KeyManager keyManager, string coordinatorIdentifier, CancellationToken cancellationToken)
		{
			Interlocked.Increment(ref _asked);
			WhenAsked?.Invoke();
			return Answer.Task;
		}
	}

	/// <summary>Authorized when the round starts; the tracker reads the fee cap while it creates the round, which is when this device goes away.</summary>
	private sealed class RoundLosingKeyChain(bool authorizationLost) : IKeyChain
	{
		public bool NeedsAuthorization { get; private set; }

		public FeeRate? MaxMiningFeeRate
		{
			get
			{
				NeedsAuthorization = authorizationLost;
				return null;
			}
		}

		public OwnershipProof GetOwnershipProof(IDestination destination, CoinJoinInputCommitmentData committedData) => throw new NotSupportedException();
		public Transaction Sign(TransactionWithPrecomputedData unsignedCoinJoin, Coin coin) => throw new NotSupportedException();
	}

	private sealed class NoRoundsRequestHandler : IWabiSabiApiRequestHandler
	{
		public Task<RoundStateResponse> GetStatusAsync(RoundStateRequest request, CancellationToken cancellationToken) => Task.FromResult(new RoundStateResponse([]));
		public Task<InputRegistrationResponse> RegisterInputAsync(InputRegistrationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
		public Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
		public Task RegisterOutputAsync(OutputRegistrationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
		public Task RemoveInputAsync(InputsRemovalRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
		public Task<ReissueCredentialResponse> ReissuanceAsync(ReissueCredentialRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
		public Task SignTransactionAsync(TransactionSignaturesRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
		public Task ReadyToSignAsync(ReadyToSignRequestRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
	}
}
