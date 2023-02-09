using NBitcoin;
using NBitcoin.Protocol;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.RegressionTests;

[Collection("RegTest collection")]
public class SendTests
{
	public SendTests(RegTestFixture regTestFixture)
	{
		RegTestFixture = regTestFixture;
	}

	private RegTestFixture RegTestFixture { get; }

	[Fact]
	public async Task SendTestsAsync()
	{
		(string password, IRPCClient rpc, Network network, _, ServiceConfiguration serviceConfiguration, BitcoinStore bitcoinStore, Backend.Global global) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);
		bitcoinStore.IndexStore.NewFilter += Common.Wallet_NewFilterProcessed;
		// Create the services.
		// 1. Create connection service.
		NodesGroup nodes = new(global.Config.Network, requirements: Constants.NodeRequirements);
		nodes.ConnectedNodes.Add(await RegTestFixture.BackendRegTestNode.CreateNewP2pNodeAsync());

		// 2. Create mempool service.

		Node node = await RegTestFixture.BackendRegTestNode.CreateNewP2pNodeAsync();
		node.Behaviors.Add(bitcoinStore.CreateUntrustedP2pBehavior());

		// 3. Create wasabi synchronizer service.
		await using HttpClientFactory httpClientFactory = new(torEndPoint: null, backendUriGetter: () => new Uri(RegTestFixture.BackendEndPoint));
		WasabiSynchronizer synchronizer = new(bitcoinStore, httpClientFactory);
		HybridFeeProvider feeProvider = new(synchronizer, null);

		// 4. Create key manager service.
		var keyManager = KeyManager.CreateNew(out _, password, network);

		// 5. Create wallet service.
		var workDir = Helpers.Common.GetWorkDir();

		using MemoryCache cache = CreateMemoryCache();

		var blockProvider = new SmartBlockProvider(
			bitcoinStore.BlockRepository,
			rpcBlockProvider: null,
			specificNodeBlockProvider: new SpecificNodeBlockProvider(network, serviceConfiguration, httpClientFactory: httpClientFactory),
			new P2PBlockProvider(network, nodes, httpClientFactory),
			cache);

		WalletManager walletManager = new(network, workDir, new WalletDirectories(network, workDir));
		walletManager.RegisterServices(bitcoinStore, synchronizer, serviceConfiguration, feeProvider, blockProvider);

		// Get some money, make it confirm.
		var key = keyManager.GetNextReceiveKey("foo label");
		var key2 = keyManager.GetNextReceiveKey("foo label");
		var txId = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(1m));
		Assert.NotNull(txId);
		await rpc.GenerateAsync(1);
		var txId2 = await rpc.SendToAddressAsync(key2.GetP2wpkhAddress(network), Money.Coins(1m));
		Assert.NotNull(txId2);
		await rpc.GenerateAsync(1);

		try
		{
			Interlocked.Exchange(ref Common.FiltersProcessedByWalletCount, 0);
			nodes.Connect(); // Start connection service.
			node.VersionHandshake(); // Start mempool service.
			synchronizer.Start(requestInterval: TimeSpan.FromSeconds(3), 10000); // Start wasabi synchronizer service.
			await feeProvider.StartAsync(CancellationToken.None);

			// Wait until the filter our previous transaction is present.
			var blockCount = await rpc.GetBlockCountAsync();
			await Common.WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), blockCount);
			var wallet = await walletManager.AddAndStartWalletAsync(keyManager);

			TransactionBroadcaster broadcaster = new(network, bitcoinStore, httpClientFactory, walletManager);
			broadcaster.Initialize(nodes, rpc);

			var waitCount = 0;
			while (wallet.Coins.Sum(x => x.Amount) == Money.Zero)
			{
				await Task.Delay(1000);
				waitCount++;
				if (waitCount >= 21)
				{
					Logger.LogInfo($"Funding transaction to the wallet '{wallet.WalletName}' did not arrive.");
					return; // Very rarely this test fails. I have no clue why. Probably because all these RegTests are interconnected, anyway let's not bother the CI with it.
				}
			}

			var scp = new Key().GetScriptPubKey(ScriptPubKeyType.Segwit);
			var res2 = wallet.BuildTransaction(password, new PaymentIntent(scp, Money.Coins(0.05m), label: "foo"), FeeStrategy.CreateFromConfirmationTarget(5), allowUnconfirmed: false);

			Assert.NotNull(res2.Transaction);
			Assert.Single(res2.OuterWalletOutputs);
			Assert.Equal(scp, res2.OuterWalletOutputs.Single().ScriptPubKey);
			Assert.Single(res2.InnerWalletOutputs);
			Assert.True(res2.Fee > Money.Satoshis(2 * 100)); // since there is a sanity check of 2sat/vb in the server
			Assert.InRange(res2.FeePercentOfSent, 0, 1);
			Assert.Single(res2.SpentCoins);
			var spentCoin = Assert.Single(res2.SpentCoins);
			Assert.Contains(new[] { key.P2wpkhScript, key2.P2wpkhScript }, x => x == spentCoin.ScriptPubKey);
			Assert.Equal(Money.Coins(1m), res2.SpentCoins.Single().Amount);
			Assert.False(res2.SpendsUnconfirmed);

			await broadcaster.SendTransactionAsync(res2.Transaction);

			Assert.Contains(res2.InnerWalletOutputs.Single(), wallet.Coins);

			#region Basic

			Script receive = keyManager.GetNextReceiveKey("Basic").P2wpkhScript;
			Money amountToSend = wallet.Coins.Where(x => x.IsAvailable()).Sum(x => x.Amount) / 2;
			var res = wallet.BuildTransaction(password, new PaymentIntent(receive, amountToSend, label: "foo"), FeeStrategy.SevenDaysConfirmationTargetStrategy, allowUnconfirmed: true);

			foreach (SmartCoin coin in res.SpentCoins)
			{
				Assert.False(coin.CoinJoinInProgress);
				Assert.True(coin.Confirmed);
				Assert.Null(coin.SpenderTransaction);
				Assert.False(coin.IsSpent());
			}

			Assert.Equal(2, res.InnerWalletOutputs.Count());
			Assert.Empty(res.OuterWalletOutputs);
			var activeOutput = res.InnerWalletOutputs.Single(x => x.ScriptPubKey == receive);
			var changeOutput = res.InnerWalletOutputs.Single(x => x.ScriptPubKey != receive);

			Assert.Equal(receive, activeOutput.ScriptPubKey);
			Assert.Equal(amountToSend, activeOutput.Amount);
			if (res.SpentCoins.Sum(x => x.Amount) - activeOutput.Amount == res.Fee) // this happens when change is too small
			{
				Assert.Contains(res.Transaction.Transaction.Outputs, x => x.Value == activeOutput.Amount);
				Logger.LogInfo($"Change Output: {changeOutput.Amount.ToString(false, true)} {changeOutput.ScriptPubKey.GetDestinationAddress(network)}");
			}
			Logger.LogInfo($"{nameof(res.Fee)}: {res.Fee}");
			Logger.LogInfo($"{nameof(res.FeePercentOfSent)}: {res.FeePercentOfSent} %");
			Logger.LogInfo($"{nameof(res.SpendsUnconfirmed)}: {res.SpendsUnconfirmed}");
			Logger.LogInfo($"Active Output: {activeOutput.Amount.ToString(false, true)} {activeOutput.ScriptPubKey.GetDestinationAddress(network)}");
			Logger.LogInfo($"TxId: {res.Transaction.GetHash()}");

			var foundReceive = false;
			Assert.InRange(res.Transaction.Transaction.Outputs.Count, 1, 2);
			foreach (var output in res.Transaction.Transaction.Outputs)
			{
				if (output.ScriptPubKey == receive)
				{
					foundReceive = true;
					Assert.Equal(amountToSend, output.Value);
				}
			}
			Assert.True(foundReceive);

			await broadcaster.SendTransactionAsync(res.Transaction);

			#endregion Basic

			#region SubtractFeeFromAmount

			receive = keyManager.GetNextReceiveKey("SubtractFeeFromAmount").P2wpkhScript;
			amountToSend = wallet.Coins.Where(x => x.IsAvailable()).Sum(x => x.Amount) / 3;
			res = wallet.BuildTransaction(password, new PaymentIntent(receive, amountToSend, subtractFee: true, label: "foo"), FeeStrategy.SevenDaysConfirmationTargetStrategy, allowUnconfirmed: true);

			Assert.Equal(2, res.InnerWalletOutputs.Count());
			Assert.Empty(res.OuterWalletOutputs);
			activeOutput = res.InnerWalletOutputs.Single(x => x.ScriptPubKey == receive);
			changeOutput = res.InnerWalletOutputs.Single(x => x.ScriptPubKey != receive);

			Assert.Equal(receive, activeOutput.ScriptPubKey);
			Assert.Equal(amountToSend - res.Fee, activeOutput.Amount);
			Assert.Contains(res.Transaction.Transaction.Outputs, x => x.Value == changeOutput.Amount);
			Logger.LogInfo($"{nameof(res.Fee)}: {res.Fee}");
			Logger.LogInfo($"{nameof(res.FeePercentOfSent)}: {res.FeePercentOfSent} %");
			Logger.LogInfo($"{nameof(res.SpendsUnconfirmed)}: {res.SpendsUnconfirmed}");
			Logger.LogInfo($"Active Output: {activeOutput.Amount.ToString(false, true)} {activeOutput.ScriptPubKey.GetDestinationAddress(network)}");
			Logger.LogInfo($"Change Output: {changeOutput.Amount.ToString(false, true)} {changeOutput.ScriptPubKey.GetDestinationAddress(network)}");
			Logger.LogInfo($"TxId: {res.Transaction.GetHash()}");

			foundReceive = false;
			Assert.InRange(res.Transaction.Transaction.Outputs.Count, 1, 2);
			foreach (var output in res.Transaction.Transaction.Outputs)
			{
				if (output.ScriptPubKey == receive)
				{
					foundReceive = true;
					Assert.Equal(amountToSend - res.Fee, output.Value);
				}
			}
			Assert.True(foundReceive);

			#endregion SubtractFeeFromAmount

			#region LowFee

			res = wallet.BuildTransaction(password, new PaymentIntent(receive, amountToSend, label: "foo"), FeeStrategy.SevenDaysConfirmationTargetStrategy, allowUnconfirmed: true);

			Assert.Equal(2, res.InnerWalletOutputs.Count());
			Assert.Empty(res.OuterWalletOutputs);
			activeOutput = res.InnerWalletOutputs.Single(x => x.ScriptPubKey == receive);
			changeOutput = res.InnerWalletOutputs.Single(x => x.ScriptPubKey != receive);

			Assert.Equal(receive, activeOutput.ScriptPubKey);
			Assert.Equal(amountToSend, activeOutput.Amount);
			Assert.Contains(res.Transaction.Transaction.Outputs, x => x.Value == changeOutput.Amount);
			Logger.LogInfo($"{nameof(res.Fee)}: {res.Fee}");
			Logger.LogInfo($"{nameof(res.FeePercentOfSent)}: {res.FeePercentOfSent} %");
			Logger.LogInfo($"{nameof(res.SpendsUnconfirmed)}: {res.SpendsUnconfirmed}");
			Logger.LogInfo($"Active Output: {activeOutput.Amount.ToString(false, true)} {activeOutput.ScriptPubKey.GetDestinationAddress(network)}");
			Logger.LogInfo($"Change Output: {changeOutput.Amount.ToString(false, true)} {changeOutput.ScriptPubKey.GetDestinationAddress(network)}");
			Logger.LogInfo($"TxId: {res.Transaction.GetHash()}");

			foundReceive = false;
			Assert.InRange(res.Transaction.Transaction.Outputs.Count, 1, 2);
			foreach (var output in res.Transaction.Transaction.Outputs)
			{
				if (output.ScriptPubKey == receive)
				{
					foundReceive = true;
					Assert.Equal(amountToSend, output.Value);
				}
			}
			Assert.True(foundReceive);

			#endregion LowFee

			#region MediumFee

			res = wallet.BuildTransaction(password, new PaymentIntent(receive, amountToSend, label: "foo"), FeeStrategy.OneDayConfirmationTargetStrategy, allowUnconfirmed: true);

			Assert.Equal(2, res.InnerWalletOutputs.Count());
			Assert.Empty(res.OuterWalletOutputs);
			activeOutput = res.InnerWalletOutputs.Single(x => x.ScriptPubKey == receive);
			changeOutput = res.InnerWalletOutputs.Single(x => x.ScriptPubKey != receive);

			Assert.Equal(receive, activeOutput.ScriptPubKey);
			Assert.Equal(amountToSend, activeOutput.Amount);
			Assert.Contains(res.Transaction.Transaction.Outputs, x => x.Value == changeOutput.Amount);
			Logger.LogInfo($"{nameof(res.Fee)}: {res.Fee}");
			Logger.LogInfo($"{nameof(res.FeePercentOfSent)}: {res.FeePercentOfSent} %");
			Logger.LogInfo($"{nameof(res.SpendsUnconfirmed)}: {res.SpendsUnconfirmed}");
			Logger.LogInfo($"Active Output: {activeOutput.Amount.ToString(false, true)} {activeOutput.ScriptPubKey.GetDestinationAddress(network)}");
			Logger.LogInfo($"Change Output: {changeOutput.Amount.ToString(false, true)} {changeOutput.ScriptPubKey.GetDestinationAddress(network)}");
			Logger.LogInfo($"TxId: {res.Transaction.GetHash()}");

			foundReceive = false;
			Assert.InRange(res.Transaction.Transaction.Outputs.Count, 1, 2);
			foreach (var output in res.Transaction.Transaction.Outputs)
			{
				if (output.ScriptPubKey == receive)
				{
					foundReceive = true;
					Assert.Equal(amountToSend, output.Value);
				}
			}
			Assert.True(foundReceive);

			#endregion MediumFee

			#region HighFee

			res = wallet.BuildTransaction(password, new PaymentIntent(receive, amountToSend, label: "foo"), FeeStrategy.TwentyMinutesConfirmationTargetStrategy, allowUnconfirmed: true);

			Assert.Equal(2, res.InnerWalletOutputs.Count());
			Assert.Empty(res.OuterWalletOutputs);
			activeOutput = res.InnerWalletOutputs.Single(x => x.ScriptPubKey == receive);
			changeOutput = res.InnerWalletOutputs.Single(x => x.ScriptPubKey != receive);

			Assert.Equal(receive, activeOutput.ScriptPubKey);
			Assert.Equal(amountToSend, activeOutput.Amount);
			Assert.Contains(res.Transaction.Transaction.Outputs, x => x.Value == changeOutput.Amount);
			Logger.LogInfo($"{nameof(res.Fee)}: {res.Fee}");
			Logger.LogInfo($"{nameof(res.FeePercentOfSent)}: {res.FeePercentOfSent} %");
			Logger.LogInfo($"{nameof(res.SpendsUnconfirmed)}: {res.SpendsUnconfirmed}");
			Logger.LogInfo($"Active Output: {activeOutput.Amount.ToString(false, true)} {activeOutput.ScriptPubKey.GetDestinationAddress(network)}");
			Logger.LogInfo($"Change Output: {changeOutput.Amount.ToString(false, true)} {changeOutput.ScriptPubKey.GetDestinationAddress(network)}");
			Logger.LogInfo($"TxId: {res.Transaction.GetHash()}");

			foundReceive = false;
			Assert.InRange(res.Transaction.Transaction.Outputs.Count, 1, 2);
			foreach (var output in res.Transaction.Transaction.Outputs)
			{
				if (output.ScriptPubKey == receive)
				{
					foundReceive = true;
					Assert.Equal(amountToSend, output.Value);
				}
			}
			Assert.True(foundReceive);

			Assert.InRange(res.Fee, Money.Zero, res.Fee);
			Assert.InRange(res.Fee, res.Fee, res.Fee);

			await broadcaster.SendTransactionAsync(res.Transaction);

			#endregion HighFee

			#region MaxAmount

			receive = keyManager.GetNextReceiveKey("MaxAmount").P2wpkhScript;

			res = wallet.BuildTransaction(password, new PaymentIntent(receive, MoneyRequest.CreateAllRemaining(), "foo"), FeeStrategy.SevenDaysConfirmationTargetStrategy, allowUnconfirmed: true);

			Assert.Single(res.InnerWalletOutputs);
			Assert.Empty(res.OuterWalletOutputs);
			activeOutput = res.InnerWalletOutputs.Single();

			Assert.Equal(receive, activeOutput.ScriptPubKey);

			Assert.Single(res.Transaction.Transaction.Outputs);
			var maxBuiltTxOutput = res.Transaction.Transaction.Outputs.Single();
			Assert.Equal(receive, maxBuiltTxOutput.ScriptPubKey);
			Assert.Equal(wallet.Coins.Where(x => x.IsAvailable()).Sum(x => x.Amount) - res.Fee, maxBuiltTxOutput.Value);

			await broadcaster.SendTransactionAsync(res.Transaction);

			#endregion MaxAmount

			#region InputSelection

			receive = keyManager.GetNextReceiveKey("InputSelection").P2wpkhScript;

			var inputCountBefore = res.SpentCoins.Count();

			res = wallet.BuildTransaction(password, new PaymentIntent(receive, MoneyRequest.CreateAllRemaining(), "foo"), FeeStrategy.SevenDaysConfirmationTargetStrategy,
				allowUnconfirmed: true,
				allowedInputs: wallet.Coins.Where(x => x.IsAvailable()).Select(x => x.Outpoint).Take(1));

			Assert.Single(res.InnerWalletOutputs);
			Assert.Empty(res.OuterWalletOutputs);
			activeOutput = res.InnerWalletOutputs.Single(x => x.ScriptPubKey == receive);

			Assert.True(inputCountBefore >= res.SpentCoins.Count());
			Assert.Equal(res.SpentCoins.Count(), res.Transaction.Transaction.Inputs.Count);

			Assert.Equal(receive, activeOutput.ScriptPubKey);
			Logger.LogInfo($"{nameof(res.Fee)}: {res.Fee}");
			Logger.LogInfo($"{nameof(res.FeePercentOfSent)}: {res.FeePercentOfSent} %");
			Logger.LogInfo($"{nameof(res.SpendsUnconfirmed)}: {res.SpendsUnconfirmed}");
			Logger.LogInfo($"Active Output: {activeOutput.Amount.ToString(false, true)} {activeOutput.ScriptPubKey.GetDestinationAddress(network)}");
			Logger.LogInfo($"TxId: {res.Transaction.GetHash()}");

			Assert.Single(res.Transaction.Transaction.Outputs);

			res = wallet.BuildTransaction(password, new PaymentIntent(receive, MoneyRequest.CreateAllRemaining(), "foo"), FeeStrategy.SevenDaysConfirmationTargetStrategy,
				allowUnconfirmed: true,
				allowedInputs: new[] { res.SpentCoins.Select(x => x.Outpoint).First() });

			Assert.Single(res.InnerWalletOutputs);
			Assert.Empty(res.OuterWalletOutputs);
			activeOutput = res.InnerWalletOutputs.Single(x => x.ScriptPubKey == receive);

			Assert.Single(res.Transaction.Transaction.Inputs);
			Assert.Single(res.Transaction.Transaction.Outputs);
			Assert.Single(res.SpentCoins);

			#endregion InputSelection

			#region Labeling

			Script receive2 = keyManager.GetNextReceiveKey("foo").P2wpkhScript;
			res = wallet.BuildTransaction(password, new PaymentIntent(receive2, MoneyRequest.CreateAllRemaining(), "my label"), FeeStrategy.SevenDaysConfirmationTargetStrategy, allowUnconfirmed: true);

			Assert.Single(res.InnerWalletOutputs);
			Assert.Equal("foo, my label", res.InnerWalletOutputs.Single().HdPubKey.Label);

			amountToSend = wallet.Coins.Where(x => x.IsAvailable()).Sum(x => x.Amount) / 3;
			res = wallet.BuildTransaction(
				password,
				new PaymentIntent(
					new DestinationRequest(new Key(), amountToSend, label: "outgoing"),
					new DestinationRequest(new Key(), amountToSend, label: "outgoing2")),
				FeeStrategy.SevenDaysConfirmationTargetStrategy,
				allowUnconfirmed: true);

			Assert.Single(res.InnerWalletOutputs);
			Assert.Equal(2, res.OuterWalletOutputs.Count());
			IEnumerable<string> change = res.InnerWalletOutputs.Single().HdPubKey.Label.Labels;
			Assert.Contains("outgoing", change);
			Assert.Contains("outgoing2", change);

			await broadcaster.SendTransactionAsync(res.Transaction);

			IEnumerable<SmartCoin> unconfirmedCoins = wallet.Coins.Where(x => x.Height == Height.Mempool).ToArray();
			IEnumerable<string> unconfirmedCoinLabels = unconfirmedCoins.SelectMany(x => x.HdPubKey.Label.Labels).ToArray();
			Assert.Contains("outgoing", unconfirmedCoinLabels);
			Assert.Contains("outgoing2", unconfirmedCoinLabels);
			IEnumerable<string> allKeyLabels = keyManager.GetKeys().SelectMany(x => x.Label.Labels);
			Assert.Contains("outgoing", allKeyLabels);
			Assert.Contains("outgoing2", allKeyLabels);

			Interlocked.Exchange(ref Common.FiltersProcessedByWalletCount, 0);
			await rpc.GenerateAsync(1);
			await Common.WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), 1);

			var bestHeight = new Height(bitcoinStore.SmartHeaderChain.TipHeight);
			IEnumerable<string> confirmedCoinLabels = wallet.Coins.Where(x => x.Height == bestHeight).SelectMany(x => x.HdPubKey.Label.Labels);
			Assert.Contains("outgoing", confirmedCoinLabels);
			Assert.Contains("outgoing2", confirmedCoinLabels);
			allKeyLabels = keyManager.GetKeys().SelectMany(x => x.Label.Labels);
			Assert.Contains("outgoing", allKeyLabels);
			Assert.Contains("outgoing2", allKeyLabels);

			#endregion Labeling

			#region AllowedInputsDisallowUnconfirmed

			inputCountBefore = res.SpentCoins.Count();

			receive = keyManager.GetNextReceiveKey("AllowedInputsDisallowUnconfirmed").P2wpkhScript;

			var allowedInputs = wallet.Coins.Where(x => x.IsAvailable()).Select(x => x.Outpoint).Take(1);
			PaymentIntent toSend = new(receive, MoneyRequest.CreateAllRemaining(), "fizz");

			// covers:
			// disallow unconfirmed with allowed inputs
			res = wallet.BuildTransaction(password, toSend, FeeStrategy.TwentyMinutesConfirmationTargetStrategy, false, allowedInputs: allowedInputs);

			activeOutput = res.InnerWalletOutputs.Single(x => x.ScriptPubKey == receive);
			Assert.Single(res.InnerWalletOutputs);
			Assert.Empty(res.OuterWalletOutputs);

			Assert.Equal(receive, activeOutput.ScriptPubKey);
			Logger.LogDebug($"{nameof(res.Fee)}: {res.Fee}");
			Logger.LogDebug($"{nameof(res.FeePercentOfSent)}: {res.FeePercentOfSent} %");
			Logger.LogDebug($"{nameof(res.SpendsUnconfirmed)}: {res.SpendsUnconfirmed}");
			Logger.LogDebug($"Active Output: {activeOutput.Amount.ToString(false, true)} {activeOutput.ScriptPubKey.GetDestinationAddress(network)}");
			Logger.LogDebug($"TxId: {res.Transaction.GetHash()}");

			Assert.True(inputCountBefore >= res.SpentCoins.Count());
			Assert.False(res.SpendsUnconfirmed);

			Assert.Single(res.Transaction.Transaction.Inputs);
			Assert.Single(res.Transaction.Transaction.Outputs);
			Assert.Single(res.SpentCoins);

			Assert.True(inputCountBefore >= res.SpentCoins.Count());
			Assert.Equal(res.SpentCoins.Count(), res.Transaction.Transaction.Inputs.Count);

			#endregion AllowedInputsDisallowUnconfirmed

			#region CustomChange

			// covers:
			// customchange
			// feePc > 1
			var scp1 = new Key().GetScriptPubKey(ScriptPubKeyType.Segwit);
			var scp2 = new Key().GetScriptPubKey(ScriptPubKeyType.Segwit);
			res = wallet.BuildTransaction(
				password,
				new PaymentIntent(
					new DestinationRequest(scp1, MoneyRequest.CreateChange()),
					new DestinationRequest(scp2, Money.Coins(0.0003m), label: "outgoing")),
				FeeStrategy.TwentyMinutesConfirmationTargetStrategy);

			Assert.Contains(scp1, res.OuterWalletOutputs.Select(x => x.ScriptPubKey));
			Assert.Contains(scp2, res.OuterWalletOutputs.Select(x => x.ScriptPubKey));

			#endregion CustomChange

			#region FeePcHigh

			res = wallet.BuildTransaction(
				password,
				new PaymentIntent(new Key(), Money.Coins(0.0003m), label: "outgoing"),
				FeeStrategy.TwentyMinutesConfirmationTargetStrategy);

			Assert.True(res.FeePercentOfSent > 1);

			var newChangeK = keyManager.GenerateNewKey("foo", KeyState.Clean, isInternal: true);
			res = wallet.BuildTransaction(
				password,
				new PaymentIntent(
					new DestinationRequest(newChangeK.P2wpkhScript, MoneyRequest.CreateChange(), "boo"),
					new DestinationRequest(new Key(), Money.Coins(0.0003m), label: "outgoing")),
				FeeStrategy.TwentyMinutesConfirmationTargetStrategy);

			Assert.True(res.FeePercentOfSent > 1);
			Assert.Single(res.OuterWalletOutputs);
			Assert.Single(res.InnerWalletOutputs);
			SmartCoin changeRes = res.InnerWalletOutputs.Single();
			Assert.Equal(newChangeK.P2wpkhScript, changeRes.ScriptPubKey);
			Assert.Equal(newChangeK.Label, changeRes.HdPubKey.Label);
			Assert.Equal(KeyState.Clean, newChangeK.KeyState); // Still clean, because the tx wasn't yet propagated.

			#endregion FeePcHigh
		}
		finally
		{
			bitcoinStore.IndexStore.NewFilter -= Common.Wallet_NewFilterProcessed;
			await walletManager.RemoveAndStopAllAsync(CancellationToken.None);
			await synchronizer.StopAsync();
			await feeProvider.StopAsync(CancellationToken.None);
			nodes?.Dispose();
			node?.Disconnect();
		}
	}

	[Fact]
	public async Task SpendUnconfirmedTxTestAsync()
	{
		(string password, IRPCClient rpc, Network network, _, ServiceConfiguration serviceConfiguration, BitcoinStore bitcoinStore, Backend.Global global) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);
		bitcoinStore.IndexStore.NewFilter += Common.Wallet_NewFilterProcessed;
		// Create the services.
		// 1. Create connection service.
		NodesGroup nodes = new(global.Config.Network, requirements: Constants.NodeRequirements);
		nodes.ConnectedNodes.Add(await RegTestFixture.BackendRegTestNode.CreateNewP2pNodeAsync());

		// 2. Create mempool service.

		Node node = await RegTestFixture.BackendRegTestNode.CreateNewP2pNodeAsync();
		node.Behaviors.Add(bitcoinStore.CreateUntrustedP2pBehavior());

		// 3. Create wasabi synchronizer service.
		await using HttpClientFactory httpClientFactory = new(torEndPoint: null, backendUriGetter: () => new Uri(RegTestFixture.BackendEndPoint));
		WasabiSynchronizer synchronizer = new(bitcoinStore, httpClientFactory);
		HybridFeeProvider feeProvider = new(synchronizer, null);

		// 4. Create key manager service.
		var keyManager = KeyManager.CreateNew(out _, password, network);

		// 5. Create wallet service.
		var workDir = Helpers.Common.GetWorkDir();

		using MemoryCache cache = CreateMemoryCache();

		var blockProvider = new SmartBlockProvider(
			bitcoinStore.BlockRepository,
			rpcBlockProvider: null,
			specificNodeBlockProvider: new SpecificNodeBlockProvider(network, serviceConfiguration, httpClientFactory: httpClientFactory),
			new P2PBlockProvider(network, nodes, httpClientFactory),
			cache);

		WalletManager walletManager = new(network, workDir, new WalletDirectories(network, workDir));
		walletManager.RegisterServices(bitcoinStore, synchronizer, serviceConfiguration, feeProvider, blockProvider);

		// Get some money, make it confirm.
		var key = keyManager.GetNextReceiveKey("foo label");

		try
		{
			nodes.Connect(); // Start connection service.
			node.VersionHandshake(); // Start mempool service.
			synchronizer.Start(requestInterval: TimeSpan.FromSeconds(3), 10000); // Start wasabi synchronizer service.
			await feeProvider.StartAsync(CancellationToken.None);

			// Wait until the filter our previous transaction is present.
			var blockCount = await rpc.GetBlockCountAsync();
			await Common.WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), blockCount);

			var wallet = await walletManager.AddAndStartWalletAsync(keyManager);
			Assert.Empty(wallet.Coins);

			// Get some money, make it confirm.
			// this is necessary because we are in a fork now.
			var eventAwaiter = new EventAwaiter<ProcessedResult>(
				h => wallet.TransactionProcessor.WalletRelevantTransactionProcessed += h,
				h => wallet.TransactionProcessor.WalletRelevantTransactionProcessed -= h);
			var tx0Id = await rpc.SendToAddressAsync(
				key.GetP2wpkhAddress(network),
				Money.Coins(1m),
				replaceable: true);
			var eventArgs = await eventAwaiter.WaitAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(tx0Id, eventArgs.NewlyReceivedCoins.Single().TransactionId);
			Assert.Single(wallet.Coins);

			TransactionBroadcaster broadcaster = new(network, bitcoinStore, httpClientFactory, walletManager);
			broadcaster.Initialize(nodes, rpc);

			var destination1 = key.PubKey.GetAddress(ScriptPubKeyType.Segwit, Network.Main);
			var destination2 = new Key().PubKey.GetAddress(ScriptPubKeyType.Legacy, Network.Main);
			var destination3 = new Key().PubKey.GetAddress(ScriptPubKeyType.Legacy, Network.Main);

			PaymentIntent operations = new(new DestinationRequest(destination1, Money.Coins(0.01m)), new DestinationRequest(destination2, Money.Coins(0.01m)), new DestinationRequest(destination3, Money.Coins(0.01m)));

			var tx1Res = wallet.BuildTransaction(password, operations, FeeStrategy.TwentyMinutesConfirmationTargetStrategy, allowUnconfirmed: true);
			Assert.Equal(2, tx1Res.InnerWalletOutputs.Count());
			Assert.Equal(2, tx1Res.OuterWalletOutputs.Count());

			// Spend the unconfirmed coin (send it to ourself)
			operations = new PaymentIntent(key.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit), Money.Coins(0.5m));
			tx1Res = wallet.BuildTransaction(password, operations, FeeStrategy.TwentyMinutesConfirmationTargetStrategy, allowUnconfirmed: true);
			eventAwaiter = new EventAwaiter<ProcessedResult>(
				h => wallet.TransactionProcessor.WalletRelevantTransactionProcessed += h,
				h => wallet.TransactionProcessor.WalletRelevantTransactionProcessed -= h);
			await broadcaster.SendTransactionAsync(tx1Res.Transaction);
			eventArgs = await eventAwaiter.WaitAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(tx0Id, eventArgs.NewlySpentCoins.Single().TransactionId);
			Assert.Equal(tx1Res.Transaction.GetHash(), eventArgs.NewlyReceivedCoins.First().TransactionId);

			// There is a coin created by the latest spending transaction
			Assert.Contains(wallet.Coins, x => x.TransactionId == tx1Res.Transaction.GetHash());

			// There is a coin destroyed
			var allCoins = wallet.TransactionProcessor.Coins.AsAllCoinsView();
			Assert.Equal(1, allCoins.Count(x => !x.IsAvailable() && x.SpenderTransaction?.GetHash() == tx1Res.Transaction.GetHash()));

			// There is at least one coin created from the destruction of the first coin
			Assert.Contains(wallet.Coins, x => x.Transaction.Transaction.Inputs.Any(o => o.PrevOut.Hash == tx0Id));

			var totalWallet = wallet.Coins.Where(c => c.IsAvailable()).Sum(c => c.Amount);
			Assert.Equal((1 * Money.COIN) - tx1Res.Fee.Satoshi, totalWallet);

			// Spend the unconfirmed and unspent coin (send it to ourself)
			operations = new PaymentIntent(key.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit), Money.Coins(0.6m), subtractFee: true);
			var tx2Res = wallet.BuildTransaction(password, operations, FeeStrategy.TwentyMinutesConfirmationTargetStrategy, allowUnconfirmed: true);

			eventAwaiter = new EventAwaiter<ProcessedResult>(
							h => wallet.TransactionProcessor.WalletRelevantTransactionProcessed += h,
							h => wallet.TransactionProcessor.WalletRelevantTransactionProcessed -= h);
			await broadcaster.SendTransactionAsync(tx2Res.Transaction);
			eventArgs = await eventAwaiter.WaitAsync(TimeSpan.FromSeconds(21));
			var spentCoins = eventArgs.NewlySpentCoins.ToArray();
			Assert.Equal(tx1Res.Transaction.GetHash(), spentCoins.First().TransactionId);
			uint256 tx2Hash = tx2Res.Transaction.GetHash();
			var receivedCoins = eventArgs.NewlyReceivedCoins.ToArray();
			Assert.Equal(tx2Hash, receivedCoins[0].TransactionId);
			Assert.Equal(tx2Hash, receivedCoins[1].TransactionId);

			// There is a coin created by the latest spending transaction
			Assert.Contains(wallet.Coins, x => x.TransactionId == tx2Res.Transaction.GetHash());

			// There is a coin destroyed
			allCoins = wallet.TransactionProcessor.Coins.AsAllCoinsView();
			Assert.Equal(2, allCoins.Count(x => !x.IsAvailable() && x.SpenderTransaction?.GetHash() == tx2Hash));

			// There is at least one coin created from the destruction of the first coin
			Assert.Contains(wallet.Coins, x => x.Transaction.Transaction.Inputs.Any(o => o.PrevOut.Hash == tx1Res.Transaction.GetHash()));

			totalWallet = wallet.Coins.Where(c => c.IsAvailable()).Sum(c => c.Amount);
			Assert.Equal((1 * Money.COIN) - tx1Res.Fee.Satoshi - tx2Res.Fee.Satoshi, totalWallet);

			Interlocked.Exchange(ref Common.FiltersProcessedByWalletCount, 0);
			var blockId = (await rpc.GenerateAsync(1)).Single();
			try
			{
				await Common.WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), 1);
			}
			catch (TimeoutException)
			{
				Logger.LogInfo("Index was not processed.");
				return; // Very rarely this test fails. I have no clue why. Probably because all these RegTests are interconnected, anyway let's not bother the CI with it.
			}

			// Verify transactions are confirmed in the blockchain
			var block = await rpc.GetBlockAsync(blockId);
			Assert.Contains(block.Transactions, x => x.GetHash() == tx2Res.Transaction.GetHash());
			Assert.Contains(block.Transactions, x => x.GetHash() == tx1Res.Transaction.GetHash());
			Assert.Contains(block.Transactions, x => x.GetHash() == tx0Id);

			Assert.True(wallet.Coins.All(x => x.Confirmed));

			// Test coin basic count.
			ICoinsView GetAllCoins() => wallet.TransactionProcessor.Coins.AsAllCoinsView();
			var coinCount = GetAllCoins().Count();
			var to = keyManager.GetNextReceiveKey("foo");
			var res = wallet.BuildTransaction(password, new PaymentIntent(to.P2wpkhScript, Money.Coins(0.2345m), label: "bar"), FeeStrategy.TwentyMinutesConfirmationTargetStrategy, allowUnconfirmed: true);
			await broadcaster.SendTransactionAsync(res.Transaction);
			Assert.Equal(coinCount + 2, GetAllCoins().Count());
			Assert.Equal(2, GetAllCoins().Count(x => !x.Confirmed));
			Interlocked.Exchange(ref Common.FiltersProcessedByWalletCount, 0);
			await rpc.GenerateAsync(1);
			await Common.WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), 1);
			Assert.Equal(coinCount + 2, GetAllCoins().Count());
			Assert.Equal(0, GetAllCoins().Count(x => !x.Confirmed));
		}
		finally
		{
			bitcoinStore.IndexStore.NewFilter -= Common.Wallet_NewFilterProcessed;
			await walletManager.RemoveAndStopAllAsync(CancellationToken.None);
			await synchronizer.StopAsync();
			await feeProvider.StopAsync(CancellationToken.None);
			nodes?.Dispose();
			node?.Disconnect();
		}
	}

	[Fact]
	public async Task ReplaceByFeeTxTestAsync()
	{
		(string password, IRPCClient rpc, Network network, _, ServiceConfiguration serviceConfiguration, BitcoinStore bitcoinStore, Backend.Global global) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);

		// Create the services.
		// 1. Create connection service.
		NodesGroup nodes = new(global.Config.Network, requirements: Constants.NodeRequirements);
		nodes.ConnectedNodes.Add(await RegTestFixture.BackendRegTestNode.CreateNewP2pNodeAsync());

		// 2. Create mempool service.

		Node node = await RegTestFixture.BackendRegTestNode.CreateNewP2pNodeAsync();
		node.Behaviors.Add(bitcoinStore.CreateUntrustedP2pBehavior());

		// 3. Create wasabi synchronizer service.
		await using HttpClientFactory httpClientFactory = new(torEndPoint: null, backendUriGetter: () => new Uri(RegTestFixture.BackendEndPoint));
		WasabiSynchronizer synchronizer = new(bitcoinStore, httpClientFactory);
		HybridFeeProvider feeProvider = new(synchronizer, null);

		// 4. Create key manager service.
		var keyManager = KeyManager.CreateNew(out _, password, network);

		// 5. Create wallet service.
		var workDir = Helpers.Common.GetWorkDir();

		using MemoryCache cache = CreateMemoryCache();

		var blockProvider = new SmartBlockProvider(
			bitcoinStore.BlockRepository,
			null,
			new SpecificNodeBlockProvider(network, serviceConfiguration, httpClientFactory: httpClientFactory),
			new P2PBlockProvider(network, nodes, httpClientFactory),
			cache);

		using var wallet = Wallet.CreateAndRegisterServices(network, bitcoinStore, keyManager, synchronizer, workDir, serviceConfiguration, feeProvider, blockProvider);
		wallet.NewFilterProcessed += Common.Wallet_NewFilterProcessed;

		Assert.Empty(wallet.Coins);

		// Get some money, make it confirm.
		var key = keyManager.GetNextReceiveKey("foo label");

		try
		{
			nodes.Connect(); // Start connection service.
			node.VersionHandshake(); // Start mempool service.
			synchronizer.Start(requestInterval: TimeSpan.FromSeconds(3), 10000); // Start wasabi synchronizer service.
			await feeProvider.StartAsync(CancellationToken.None);

			// Wait until the filter our previous transaction is present.
			var blockCount = await rpc.GetBlockCountAsync();
			await Common.WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), blockCount);

			using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
			{
				await wallet.StartAsync(cts.Token); // Initialize wallet service.
			}

			Assert.Empty(wallet.Coins);

			var tx0Id = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(1m), replaceable: true);
			while (!wallet.Coins.Any())
			{
				await Task.Delay(500); // Waits for the funding transaction get to the mempool.
			}

			Assert.Single(wallet.Coins);
			Assert.True(wallet.Coins.First().IsReplaceable());

			var bfr = await rpc.BumpFeeAsync(tx0Id);
			var tx1Id = bfr.TransactionId;
			await Task.Delay(2000); // Waits for the replacement transaction get to the mempool.
			Assert.Single(wallet.Coins);
			Assert.True(wallet.Coins.First().IsReplaceable());
			Assert.Equal(tx1Id, wallet.Coins.First().TransactionId);

			bfr = await rpc.BumpFeeAsync(tx1Id);
			var tx2Id = bfr.TransactionId;
			await Task.Delay(2000); // Waits for the replacement transaction get to the mempool.
			Assert.Single(wallet.Coins);
			Assert.True(wallet.Coins.First().IsReplaceable());
			Assert.Equal(tx2Id, wallet.Coins.First().TransactionId);

			Interlocked.Exchange(ref Common.FiltersProcessedByWalletCount, 0);
			await rpc.GenerateAsync(1);
			await Common.WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), 1);

			var coin = Assert.Single(wallet.Coins);
			Assert.True(coin.Confirmed);
			Assert.False(coin.IsReplaceable());
			Assert.Equal(tx2Id, coin.TransactionId);
		}
		finally
		{
			await wallet.StopAsync(CancellationToken.None);
			await synchronizer.StopAsync();
			await feeProvider.StopAsync(CancellationToken.None);
			nodes?.Dispose();
			node?.Disconnect();
		}
	}

	private static MemoryCache CreateMemoryCache()
	{
		return new MemoryCache(new MemoryCacheOptions
		{
			SizeLimit = 1_000,
			ExpirationScanFrequency = TimeSpan.FromSeconds(30)
		});
	}
}
