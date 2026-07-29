using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Payjoin;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.UnitTests.ViewModels.TestDoubles;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.PayJoin;
using Xunit;
using PjFfi = global::Payjoin;
using WasabiWallet = WalletWasabi.Wallets.Wallet;

namespace WalletWasabi.Tests.UnitTests.Payjoin;

/// <summary>
/// Regression tests for the send screen's payjoin dispatch path (WU-Q1): pasting a
/// BIP 21 URI with a <c>pj=</c> endpoint rewrites To to the bare address, and the
/// To-subscription re-parse used to reset <c>PayJoinEndPoint</c> (and
/// <c>_parsedAddress</c>) right after the parse armed them — every GUI payjoin send
/// silently degraded to a plain send. These tests drive the real
/// <see cref="SendViewModel"/> wiring above the TransactionFactory seam the existing
/// round-trip tests use.
/// </summary>
public class SendViewModelPayjoinTests
{
	private sealed record Harness(
		SendViewModel Vm,
		FakeServices Services,
		PayjoinSenderManager Manager,
		PayjoinSenderSessionStore SenderStore,
		BitcoinAddress Destination,
		string Endpoint,
		string Bip21) : IDisposable
	{
		public void Dispose() => Manager.Dispose();
	}

	private static async Task<Harness> CreateHarnessAsync(
		string[]? cliArgs = null,
		[CallerFilePath] string callerFilePath = "",
		[CallerMemberName] string callerMemberName = "")
	{
		string workDir = await Common.GetEmptyWorkDirAsync(callerFilePath, callerMemberName);

		using Key destinationKey = new();
		BitcoinAddress destination = destinationKey.PubKey.GetAddress(ScriptPubKeyType.Segwit, Network.Main);
		using PjFfi.PjUri pjUri = PayjoinFfiTestHelpers.CreatePjUri(destination.ToString());
		string endpoint = pjUri.PjEndpoint();
		string bip21 = $"bitcoin:{destination}?amount=0.001&pj={Uri.EscapeDataString(endpoint)}";

		var wallet = await CreateWalletAsync(workDir);

		var services = new FakeServices(workDir, cliArgs);
		var senderStore = PayjoinSenderSessionStore.FromFile(SqliteStorageHelper.InMemoryDatabase);
#pragma warning disable CA2000 // Ownership is transferred to the returned Harness, which disposes it.
		var manager = new PayjoinSenderManager(senderStore, Network.Main, _ => Task.CompletedTask, _ => false);
#pragma warning restore CA2000
		services.AddHostedService(manager);

		var uiContext = FakeUiContext.Create(services);
		var walletModel = new FakeWalletModel(wallet, uiContext.AmountProvider);
		var parameters = new SendFlowModel(wallet, walletModel, donate: false);
		var vm = new SendViewModel(uiContext, walletModel, parameters, (_, _) => Task.FromResult<string?>(null));

		return new Harness(vm, services, manager, senderStore, destination, endpoint, bip21);
	}

	private static async Task<WasabiWallet> CreateWalletAsync(string workDir)
	{
		var eventBus = new EventBus();
		var filterHeaderChain = new FilterHeaderChain();
#pragma warning disable CA2000 // Ownership of the wallet's stores ends with the test process.
		var filterStore = new FilterStore(Path.Combine(workDir, "Filters"), Network.Main, filterHeaderChain, eventBus);
		var transactionStore = new AllTransactionStore(SqliteStorageHelper.InMemoryDatabase, Network.Main);
#pragma warning restore CA2000
		await transactionStore.InitializeAsync();

		var factory = WasabiWallet.CreateFactory(
			Network.Main,
			filterStore,
			transactionStore,
			filterHeaderChain,
			new MempoolService(eventBus),
			new ServiceConfiguration(Money.Coins(0.0001m)),
			(_, _) => Task.FromResult<Block?>(null),
			eventBus,
			new CpfpInfoProvider(Workers.Spawn(
				$"CpfpInfoProvider_{Guid.NewGuid():N}",
				Workers.EventDriven(Unit.Instance, CpfpInfoUpdater.CreateForRegTest()))));

		return factory(KeyManager.CreateNew(out _, password: "", Network.Main));
	}

	private static IEnumerable<ErrorDescriptor> GetToErrors(SendViewModel vm) =>
		((INotifyDataErrorInfo)vm).GetErrors(nameof(SendViewModel.To)).OfType<ErrorDescriptor>();

	[Fact]
	public async Task PastedPayjoinBip21KeepsEndpointArmedThroughReparseAsync()
	{
		using Harness harness = await CreateHarnessAsync();

		// Paste: the To subscription parses the URI, rewrites To to the bare address and
		// queues a re-parse of that bare address behind the current scheduler action.
		harness.Vm.To = harness.Bip21;

		Assert.Equal(harness.Destination.ToString(), harness.Vm.To);
		Assert.Equal(harness.Endpoint, harness.Vm.PayJoinEndPoint);
		Assert.True(harness.Vm.IsPayJoin);

		// The exact call the send flow makes at dispatch time (OnNextAsync) must yield a
		// live BIP 77 client, not null.
		IPayjoinClient? client = harness.Vm.GetPayjoinClient(harness.Vm.PayJoinEndPoint);
		var bip77Client = Assert.IsType<Bip77PayjoinClient>(client);

		// With no relay config override, the client runs on the default relay set.
		Assert.Equal(
			PayjoinConstants.DefaultOhttpRelays.ToHashSet(),
			bip77Client.OhttpRelays.ToHashSet());
	}

	[Fact]
	public async Task ConfiguredOhttpRelaysReachTheSenderClientAsync()
	{
		string[] relays = ["https://relay-a.example", "https://relay-b.example"];
		using Harness harness = await CreateHarnessAsync(
			cliArgs: [$"--PayjoinOhttpRelays={string.Join(';', relays)}"]);

		harness.Vm.To = harness.Bip21;

		var client = Assert.IsType<Bip77PayjoinClient>(harness.Vm.GetPayjoinClient(harness.Vm.PayJoinEndPoint));

		// Order is shuffled per session, so compare as sets.
		Assert.Equal(relays.ToHashSet(), client.OhttpRelays.ToHashSet());
	}

	[Fact]
	public async Task SessionReuseWarningSurvivesToRewriteAsync()
	{
		using Harness harness = await CreateHarnessAsync();
		Assert.True(Bip77UriParams.TryGetReceiverKey(harness.Endpoint, out string? receiverKey));
		var session = harness.SenderStore.CreateSession(harness.Endpoint, receiverKey, "FakeWallet");
		harness.SenderStore.CompleteSession(session.Id);

		harness.Vm.To = harness.Bip21;

		// The pre-flight validation must still see the endpoint after To was rewritten to
		// the bare address.
		Assert.Contains(GetToErrors(harness.Vm), e =>
			e.Severity == ErrorSeverity.Warning && e.Message.Contains("already used"));
	}

	[Fact]
	public async Task ReplacingArmedPayjoinWithPlainAddressDisarmsAsync()
	{
		using Harness harness = await CreateHarnessAsync();
		harness.Vm.To = harness.Bip21;
		Assert.NotNull(harness.Vm.PayJoinEndPoint);

		// The user types an unrelated plain address over the armed payjoin.
		using Key otherKey = new();
		harness.Vm.To = otherKey.PubKey.GetAddress(ScriptPubKeyType.Segwit, Network.Main).ToString();

		Assert.Null(harness.Vm.PayJoinEndPoint);
		Assert.False(harness.Vm.IsPayJoin);
		Assert.Null(harness.Vm.GetPayjoinClient(harness.Vm.PayJoinEndPoint));
	}
}
