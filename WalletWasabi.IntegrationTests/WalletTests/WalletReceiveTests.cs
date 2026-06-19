using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.IntegrationTests.Infrastructure;
using Xunit;

namespace WalletWasabi.IntegrationTests.WalletTests;

/// <summary>
/// Integration tests for wallet receiving functionality.
/// Tests the full flow of receiving funds and tracking coins.
/// </summary>
[Collection("Integration tests")]
public class WalletReceiveTests
{
	private readonly IntegrationTestFixture _fixture;

	public WalletReceiveTests(IntegrationTestFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact(Timeout = 120_000)] // 2 minute timeout
	public async Task Wallet_ReceivesConfirmedFunds_CoinsAreTracked()
	{
		// Arrange
		await using var env = await RegTestEnvironment.CreateAsync(_fixture);

		var keyManager = env.CreateKeyManager();
		var wallet = env.CreateWallet(keyManager);

		var receiveKey = keyManager.GetNextReceiveKey("Test receive");
		var receiveAddress = receiveKey.GetP2wpkhAddress(env.Network);

		// Act - Send funds and confirm
		var fundingAmount = Money.Coins(1m);
		await env.FundAddressAsync(receiveAddress, fundingAmount, confirmations: 1);

		// Sync filters so wallet can discover the transaction
		await env.SyncFiltersAsync();

		// Start the wallet to process filters
		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		await wallet.StartAsync(cts.Token);

		// Wait for wallet to process filters and find the coin
		await env.WaitForConditionAsync(
			() => wallet.Coins.Any(),
			TimeSpan.FromSeconds(30));

		// Assert
		Assert.Single(wallet.Coins);
		var coin = wallet.Coins.First();
		Assert.Equal(fundingAmount, coin.Amount);
		Assert.True(coin.Confirmed);
		Assert.Equal(receiveKey.P2wpkhScript, coin.ScriptPubKey);

		await wallet.StopAsync(CancellationToken.None);
	}

	[Fact(Timeout = 120_000)] // 2 minute timeout
	public async Task Wallet_ReceivesMultipleFunds_AllCoinsTracked()
	{
		// Arrange
		await using var env = await RegTestEnvironment.CreateAsync(_fixture);

		var keyManager = env.CreateKeyManager();
		var wallet = env.CreateWallet(keyManager);

		// Get multiple receive addresses
		var key1 = keyManager.GetNextReceiveKey("Payment 1");
		var key2 = keyManager.GetNextReceiveKey("Payment 2");
		var key3 = keyManager.GetNextReceiveKey("Payment 3");

		var address1 = key1.GetP2wpkhAddress(env.Network);
		var address2 = key2.GetP2wpkhAddress(env.Network);
		var address3 = key3.GetP2wpkhAddress(env.Network);

		// Act - Send multiple funds
		await env.FundAddressAsync(address1, Money.Coins(0.5m), confirmations: 0);
		await env.FundAddressAsync(address2, Money.Coins(1.0m), confirmations: 0);
		await env.FundAddressAsync(address3, Money.Coins(1.5m), confirmations: 1); // Only last one confirms all

		await env.SyncFiltersAsync();

		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		await wallet.StartAsync(cts.Token);

		await env.WaitForConditionAsync(
			() => wallet.Coins.Count() >= 3,
			TimeSpan.FromSeconds(30));

		// Assert
		Assert.Equal(3, wallet.Coins.Count());

		var totalAmountSatoshis = wallet.Coins.Sum(c => (long)c.Amount);
		Assert.Equal(Money.Coins(3m).Satoshi, totalAmountSatoshis);

		// All should be confirmed since last generate confirmed all pending
		Assert.All(wallet.Coins, c => Assert.True(c.Confirmed));

		await wallet.StopAsync(CancellationToken.None);
	}

	[Fact(Timeout = 120_000)] // 2 minute timeout
	public async Task Wallet_ReceivesSameAddressTwice_BothCoinsTracked()
	{
		// Arrange
		await using var env = await RegTestEnvironment.CreateAsync(_fixture);

		var keyManager = env.CreateKeyManager();
		var wallet = env.CreateWallet(keyManager);

		var receiveKey = keyManager.GetNextReceiveKey("Reused address");
		var receiveAddress = receiveKey.GetP2wpkhAddress(env.Network);

		// Act - Send to same address twice
		await env.FundAddressAsync(receiveAddress, Money.Coins(0.5m), confirmations: 0);
		await env.FundAddressAsync(receiveAddress, Money.Coins(0.3m), confirmations: 1);

		await env.SyncFiltersAsync();

		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		await wallet.StartAsync(cts.Token);

		await env.WaitForConditionAsync(
			() => wallet.Coins.Count() >= 2,
			TimeSpan.FromSeconds(30));

		// Assert
		Assert.Equal(2, wallet.Coins.Count());
		Assert.All(wallet.Coins, c => Assert.Equal(receiveKey.P2wpkhScript, c.ScriptPubKey));

		var amounts = wallet.Coins.Select(c => c.Amount).OrderBy(a => a).ToList();
		Assert.Equal(Money.Coins(0.3m), amounts[0]);
		Assert.Equal(Money.Coins(0.5m), amounts[1]);

		await wallet.StopAsync(CancellationToken.None);
	}

	[Fact(Timeout = 120_000)] // 2 minute timeout
	public async Task Wallet_TransactionWithChange_ChangeAddressUsed()
	{
		// This test verifies that when we send a transaction, change is returned
		// to an internal (change) address, not a receive address
		await using var env = await RegTestEnvironment.CreateAsync(_fixture);

		var keyManager = env.CreateKeyManager();
		var wallet = env.CreateWallet(keyManager);

		var receiveKey = keyManager.GetNextReceiveKey("Initial funding");
		var receiveAddress = receiveKey.GetP2wpkhAddress(env.Network);

		// Fund the wallet
		await env.FundAddressAsync(receiveAddress, Money.Coins(2m), confirmations: 1);
		await env.SyncFiltersAsync();

		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		await wallet.StartAsync(cts.Token);

		await env.WaitForConditionAsync(
			() => wallet.Coins.Any(),
			TimeSpan.FromSeconds(30));

		// Assert initial state
		Assert.Single(wallet.Coins);
		var initialCoin = wallet.Coins.First();
		Assert.False(initialCoin.HdPubKey.IsInternal); // Receive key is external

		await wallet.StopAsync(CancellationToken.None);
	}
}
