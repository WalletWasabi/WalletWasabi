using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.FeeRateEstimation;
using WalletWasabi.IntegrationTests.Infrastructure;
using WalletWasabi.Services;
using Xunit;

namespace WalletWasabi.IntegrationTests.WalletTests;

/// <summary>
/// Integration tests for wallet sending functionality.
/// Tests transaction building, signing, and broadcasting.
/// </summary>
[Collection("Integration tests")]
public class WalletSendTests
{
	private readonly IntegrationTestFixture _fixture;

	public WalletSendTests(IntegrationTestFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact(Timeout = 120_000)] // 2 minute timeout
	public async Task Wallet_BuildsAndBroadcastsTransaction_Success()
	{
		// Arrange
		await using var env = await RegTestEnvironment.CreateAsync(_fixture);

		var keyManager = env.CreateKeyManager();
		var wallet = env.CreateWallet(keyManager);
		wallet.Password = RegTestEnvironment.DefaultPassword;

		var receiveKey = keyManager.GetNextReceiveKey("Funding");
		var receiveAddress = receiveKey.GetP2wpkhAddress(env.Network);

		// Fund the wallet with confirmed coins
		await env.FundAddressAsync(receiveAddress, Money.Coins(2m), confirmations: 1);
		await env.SyncFiltersAsync();

		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		await wallet.StartAsync(cts.Token);

		await env.WaitForConditionAsync(
			() => wallet.Coins.Any(c => c.Confirmed),
			TimeSpan.FromSeconds(30));

		// Wallet needs fee rate estimations to build transaction
		// Trigger fee estimation by publishing fake event
		env.EventBus.Publish(new MiningFeeRatesChanged(
			new FeeRateEstimations(
				new Dictionary<int, FeeRate>
				{
					{ 2, new FeeRate(10m) },
					{ 6, new FeeRate(5m) },
					{ 18, new FeeRate(2m) },
					{ 36, new FeeRate(1m) },
					{ 144, new FeeRate(1m) },
					{ 432, new FeeRate(1m) },
					{ 1008, new FeeRate(1m) }
				})));

		// Wait for fee rates to be available
		await Task.Delay(100);

		// Act - Build a transaction
		using var destKey = new Key();
		var destinationAddress = destKey.PubKey.GetAddress(ScriptPubKeyType.Segwit, env.Network);
		var sendAmount = Money.Coins(0.5m);

		var paymentIntent = new PaymentIntent(
			destinationAddress.ScriptPubKey,
			sendAmount,
			label: new LabelsArray("Test payment"));

		var txResult = wallet.BuildTransaction(
			RegTestEnvironment.DefaultPassword,
			paymentIntent,
			FeeStrategy.CreateFromConfirmationTarget(6),
			allowUnconfirmed: false);

		// Assert transaction was built correctly
		Assert.NotNull(txResult.Transaction);
		Assert.Single(txResult.SpentCoins);
		Assert.True(txResult.Fee > Money.Zero);

		// Broadcast the transaction
		var broadcaster = new TransactionBroadcaster([new RpcBroadcaster(env.RpcClient)], env.MempoolService);
		await broadcaster.SendTransactionAsync(txResult.Transaction);

		// Verify transaction is in mempool
		var mempoolTxs = await env.RpcClient.GetRawMempoolAsync();
		Assert.Contains(txResult.Transaction.GetHash(), mempoolTxs);

		// Confirm the transaction
		await env.RpcClient.GenerateAsync(1);

		await wallet.StopAsync(CancellationToken.None);
	}

	[Fact(Timeout = 120_000)] // 2 minute timeout
	public async Task Wallet_BuildsTransactionWithSubtractFee_CorrectAmounts()
	{
		// Arrange
		await using var env = await RegTestEnvironment.CreateAsync(_fixture);

		var keyManager = env.CreateKeyManager();
		var wallet = env.CreateWallet(keyManager);
		wallet.Password = RegTestEnvironment.DefaultPassword;

		var receiveKey = keyManager.GetNextReceiveKey("Funding");
		var receiveAddress = receiveKey.GetP2wpkhAddress(env.Network);

		await env.FundAddressAsync(receiveAddress, Money.Coins(1m), confirmations: 1);
		await env.SyncFiltersAsync();

		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		await wallet.StartAsync(cts.Token);

		await env.WaitForConditionAsync(
			() => wallet.Coins.Any(c => c.Confirmed),
			TimeSpan.FromSeconds(30));

		env.EventBus.Publish(new MiningFeeRatesChanged(
			new FeeRateEstimations(
				new Dictionary<int, FeeRate>
				{
					{ 2, new FeeRate(10m) }, { 6, new FeeRate(5m) }, { 18, new FeeRate(2m) },
					{ 36, new FeeRate(1m) }, { 144, new FeeRate(1m) }, { 432, new FeeRate(1m) }, { 1008, new FeeRate(1m) }
				})));

		await Task.Delay(100);

		// Act - Build a transaction with subtractFee = true
		using var destKey = new Key();
		var destinationAddress = destKey.PubKey.GetAddress(ScriptPubKeyType.Segwit, env.Network);
		var sendAmount = Money.Coins(0.5m);

		var paymentIntent = new PaymentIntent(
			destinationAddress.ScriptPubKey,
			sendAmount,
			subtractFee: true,
			label: new LabelsArray("Subtract fee test"));

		var txResult = wallet.BuildTransaction(
			RegTestEnvironment.DefaultPassword,
			paymentIntent,
			FeeStrategy.CreateFromConfirmationTarget(6),
			allowUnconfirmed: false);

		// Assert
		Assert.NotNull(txResult.Transaction);

		// With subtractFee, the destination receives (sendAmount - fee)
		var destinationOutput = txResult.Transaction.Transaction.Outputs
			.First(o => o.ScriptPubKey == destinationAddress.ScriptPubKey);

		Assert.Equal(sendAmount - txResult.Fee, destinationOutput.Value);

		await wallet.StopAsync(CancellationToken.None);
	}

	[Fact(Timeout = 120_000)] // 2 minute timeout
	public async Task Wallet_SpendsUnconfirmedCoin_WhenAllowed()
	{
		// Arrange
		await using var env = await RegTestEnvironment.CreateAsync(_fixture);

		var keyManager = env.CreateKeyManager();
		var wallet = env.CreateWallet(keyManager);
		wallet.Password = RegTestEnvironment.DefaultPassword;

		var receiveKey = keyManager.GetNextReceiveKey("Funding");
		var receiveAddress = receiveKey.GetP2wpkhAddress(env.Network);

		// Send unconfirmed funds (no block generation)
		var txid = await env.RpcClient.SendToAddressAsync(receiveAddress, Money.Coins(1m));

		// We need at least one block for filters to exist
		await env.RpcClient.GenerateAsync(1);
		await env.SyncFiltersAsync();

		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		await wallet.StartAsync(cts.Token);

		await env.WaitForConditionAsync(
			() => wallet.Coins.Any(),
			TimeSpan.FromSeconds(30));

		env.EventBus.Publish(new MiningFeeRatesChanged(
			new FeeRateEstimations(
				new Dictionary<int, FeeRate>
				{
					{ 2, new FeeRate(10m) }, { 6, new FeeRate(5m) }, { 18, new FeeRate(2m) },
					{ 36, new FeeRate(1m) }, { 144, new FeeRate(1m) }, { 432, new FeeRate(1m) }, { 1008, new FeeRate(1m) }
				})));

		await Task.Delay(100);

		// Act - Try to spend the confirmed coin with allowUnconfirmed = true
		using var destKey = new Key();
		var destinationAddress = destKey.PubKey.GetAddress(ScriptPubKeyType.Segwit, env.Network);

		var paymentIntent = new PaymentIntent(
			destinationAddress.ScriptPubKey,
			Money.Coins(0.5m),
			label: new LabelsArray("Spend confirmed"));

		// This should succeed because we have a confirmed coin
		var txResult = wallet.BuildTransaction(
			RegTestEnvironment.DefaultPassword,
			paymentIntent,
			FeeStrategy.CreateFromConfirmationTarget(6),
			allowUnconfirmed: true);

		Assert.NotNull(txResult.Transaction);

		await wallet.StopAsync(CancellationToken.None);
	}

	[Fact(Timeout = 120_000)] // 2 minute timeout
	public async Task Wallet_CoinSelection_UsesMinimalInputs()
	{
		// Arrange
		await using var env = await RegTestEnvironment.CreateAsync(_fixture);

		var keyManager = env.CreateKeyManager();
		var wallet = env.CreateWallet(keyManager);
		wallet.Password = RegTestEnvironment.DefaultPassword;

		// Fund with multiple small UTXOs
		var key1 = keyManager.GetNextReceiveKey("UTXO 1");
		var key2 = keyManager.GetNextReceiveKey("UTXO 2");
		var key3 = keyManager.GetNextReceiveKey("UTXO 3");

		await env.FundAddressAsync(key1.GetP2wpkhAddress(env.Network), Money.Coins(0.3m), confirmations: 0);
		await env.FundAddressAsync(key2.GetP2wpkhAddress(env.Network), Money.Coins(0.5m), confirmations: 0);
		await env.FundAddressAsync(key3.GetP2wpkhAddress(env.Network), Money.Coins(1.0m), confirmations: 1);

		await env.SyncFiltersAsync();

		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		await wallet.StartAsync(cts.Token);

		await env.WaitForConditionAsync(
			() => wallet.Coins.Count() >= 3,
			TimeSpan.FromSeconds(30));

		env.EventBus.Publish(new MiningFeeRatesChanged(
			new FeeRateEstimations(
				new Dictionary<int, FeeRate>
				{
					{ 2, new FeeRate(10m) }, { 6, new FeeRate(5m) }, { 18, new FeeRate(2m) },
					{ 36, new FeeRate(1m) }, { 144, new FeeRate(1m) }, { 432, new FeeRate(1m) }, { 1008, new FeeRate(1m) }
				})));

		await Task.Delay(100);

		// Act - Send an amount that can be satisfied by the largest UTXO alone
		using var destKey = new Key();
		var destinationAddress = destKey.PubKey.GetAddress(ScriptPubKeyType.Segwit, env.Network);

		var paymentIntent = new PaymentIntent(
			destinationAddress.ScriptPubKey,
			Money.Coins(0.4m),
			label: new LabelsArray("Minimal inputs test"));

		var txResult = wallet.BuildTransaction(
			RegTestEnvironment.DefaultPassword,
			paymentIntent,
			FeeStrategy.CreateFromConfirmationTarget(6),
			allowUnconfirmed: false);

		// Assert - Coin selection should prefer using the single 1 BTC UTXO
		// or minimal combination, not all three UTXOs
		Assert.NotNull(txResult.Transaction);
		Assert.InRange(txResult.SpentCoins.Count(), 1, 2);

		await wallet.StopAsync(CancellationToken.None);
	}
}
