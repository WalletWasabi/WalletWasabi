using NBitcoin;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Wallet;

/// <summary>
/// Tests for wallet synchronization.
/// </summary>
/// <seealso cref="SyncType"/>
public class WalletSynchronizationTests
{
	/// <summary>
	/// Receive on an internal key then spend (-> Key in subset SyncType.NonTurbo) then receive again.
	/// Verifies that the wallet won't find the last TX during Turbo sync but will find it during NonTurbo.
	/// </summary>
	[Fact]
	public async Task InternalAddressReuseNoBlockOverlapTestAsync()
	{
		using CancellationTokenSource testDeadlineCts = new(TimeSpan.FromMinutes(5));

		var node = await MockNode.CreateNodeAsync();
		var minerWallet = node.Wallet;
		var wallet = new TestWallet("wallet", node.Rpc);

		var minerFirstKeyScript = minerWallet.GetNextDestination();
		var firstInternalKeyScript = wallet.GetNextInternalDestination();

		// First receive.
		await SendToAsync(minerWallet, wallet, Money.Coins(1), firstInternalKeyScript, node, testDeadlineCts.Token);

		// Send the money away.
		await SendToAsync(wallet, minerWallet, Money.Coins(1), minerFirstKeyScript, node, testDeadlineCts.Token);

		// Address re-use.
		await SendToAsync(minerWallet, wallet, Money.Coins(2), firstInternalKeyScript, node, testDeadlineCts.Token);

		await using var builder = new WalletBuilder(node);
		using var realWallet = await builder.CreateRealWalletBasedOnTestWalletAsync(wallet);
		await realWallet.WalletFilterProcessor.StartAsync(testDeadlineCts.Token);

		await realWallet.PerformSynchronizationAsync(SyncType.Turbo, testDeadlineCts.Token);
		Assert.Single(realWallet.GetAllCoins());

		await realWallet.PerformSynchronizationAsync(SyncType.NonTurbo, testDeadlineCts.Token);
		Assert.Equal(2, realWallet.GetAllCoins().Count());
	}

	/// <summary>
	/// Receive on an internal key then spend (-> Key in subset SyncType.NonTurbo) then receive again and spend to an external key in a different block.
	/// Verifies that the wallet will process the spend correctly when it doesn't have the coins in its CoinsRegistry at the time of spending.
	/// </summary>
	[Fact]
	public async Task InternalAddressReuseThenSpendOnExternalKeyTestAsync()
	{
		using CancellationTokenSource testDeadlineCts = new(TimeSpan.FromMinutes(5));

		var node = await MockNode.CreateNodeAsync();
		var minerWallet = node.Wallet;
		var wallet = new TestWallet("wallet", node.Rpc);

		var minerFirstKeyScript = minerWallet.GetNextDestination();
		var firstInternalKeyScript = wallet.GetNextInternalDestination();
		var walletExternalKeyScript = wallet.GetNextDestination();

		// First receive.
		await SendToAsync(minerWallet, wallet, Money.Coins(1), firstInternalKeyScript, node, testDeadlineCts.Token);

		// Send the money away.
		await SendToAsync(wallet, minerWallet, Money.Coins(1), minerFirstKeyScript, node, testDeadlineCts.Token);

		// Address re-use.
		await SendToAsync(minerWallet, wallet, Money.Coins(2), firstInternalKeyScript, node, testDeadlineCts.Token);

		// Self spend the coins to an external key.
		await SendToAsync(wallet, wallet, Money.Coins(2), walletExternalKeyScript, node, testDeadlineCts.Token);

		await using var builder = new WalletBuilder(node);
		using var realWallet = await builder.CreateRealWalletBasedOnTestWalletAsync(wallet);
		await realWallet.WalletFilterProcessor.StartAsync(testDeadlineCts.Token);
		var coins = realWallet.Coins;

		await realWallet.PerformSynchronizationAsync(SyncType.Turbo, testDeadlineCts.Token);
		Assert.Single(coins.Available());

		await realWallet.PerformSynchronizationAsync(SyncType.NonTurbo, testDeadlineCts.Token);
		Assert.Single(coins.Available());
	}

	/// <summary>
	/// Reuse 2 internal keys then send all funds away, then receive on first one, send to second one, then send on an external key.
	/// This aims to make sure that the CoinsRegistry will catch all the history.
	/// </summary>
	[Fact]
	public async Task InternalAddressReuseChainThenSpendOnExternalKeyTestAsync()
	{
		using CancellationTokenSource testDeadlineCts = new(TimeSpan.FromMinutes(5));

		var node = await MockNode.CreateNodeAsync();
		var minerWallet = node.Wallet;
		var wallet = new TestWallet("wallet", node.Rpc);

		var minerFirstKeyScript = minerWallet.GetNextDestination();
		var firstInternalKeyScript = wallet.GetNextInternalDestination();
		var secondInternalKeyScript = wallet.GetNextInternalDestination();
		var walletExternalKeyScript = wallet.GetNextDestination();

		// First address reuse and send money away
		await SendToAsync(minerWallet, wallet, Money.Coins(1), firstInternalKeyScript, node, testDeadlineCts.Token);
		await SendToAsync(wallet, minerWallet, Money.Coins(1), minerFirstKeyScript, node, testDeadlineCts.Token);
		await SendToAsync(minerWallet, wallet, Money.Coins(2), firstInternalKeyScript, node, testDeadlineCts.Token);
		await SendToAsync(wallet, minerWallet, Money.Coins(2), minerFirstKeyScript, node, testDeadlineCts.Token);

		// Second address reuse and send money away
		await SendToAsync(minerWallet, wallet, Money.Coins(1), secondInternalKeyScript, node, testDeadlineCts.Token);
		await SendToAsync(wallet, minerWallet, Money.Coins(1), minerFirstKeyScript, node, testDeadlineCts.Token);
		await SendToAsync(minerWallet, wallet, Money.Coins(2), secondInternalKeyScript, node, testDeadlineCts.Token);
		await SendToAsync(wallet, minerWallet, Money.Coins(2), minerFirstKeyScript, node, testDeadlineCts.Token);

		// Receive again on first internal key
		await SendToAsync(minerWallet, wallet, Money.Coins(3), firstInternalKeyScript, node, testDeadlineCts.Token);

		// Self spend the coins to second internal key
		await SendToAsync(wallet, wallet, Money.Coins(3), secondInternalKeyScript, node, testDeadlineCts.Token);

		// Self spend the coins to an external key
		await SendToAsync(wallet, wallet, Money.Coins(3), walletExternalKeyScript, node, testDeadlineCts.Token);

		await using var builder = new WalletBuilder(node);
		using var realWallet = await builder.CreateRealWalletBasedOnTestWalletAsync(wallet);
		await realWallet.WalletFilterProcessor.StartAsync(testDeadlineCts.Token);

		await realWallet.PerformSynchronizationAsync(SyncType.Turbo, testDeadlineCts.Token);
		Assert.Single(realWallet.Coins.Available());

		await realWallet.PerformSynchronizationAsync(SyncType.NonTurbo, testDeadlineCts.Token);
		Assert.Equal(7, realWallet.GetAllCoins().Count());
	}

	/// <summary>
	/// Receive on an internal key then spend (-> Key in subset SyncType.NonTurbo) then receive again but in the same block receive on an external key.
	/// Verifies that the wallet will find the TX reusing internal key twice (once in Turbo because of the TX on ext key in the same block and again in NonTurbo), but will process it without issues.
	/// </summary>
	[Fact]
	public async Task InternalAddressReuseWithBlockOverlapTestAsync()
	{
		using CancellationTokenSource testDeadlineCts = new(TimeSpan.FromMinutes(5));

		var node = await MockNode.CreateNodeAsync();
		var minerWallet = node.Wallet;
		var wallet = new TestWallet("wallet", node.Rpc);

		var minerFirstKeyScript = minerWallet.GetNextDestination();
		var firstInternalKeyScript = wallet.GetNextInternalDestination();
		var walletExternalKeyScript = wallet.GetNextDestination();

		// First receive.
		await SendToAsync(minerWallet, wallet, Money.Coins(1), firstInternalKeyScript, node, testDeadlineCts.Token);

		// Send the money away.
		await SendToAsync(wallet, minerWallet, Money.Coins(1), minerFirstKeyScript, node, testDeadlineCts.Token);

		// Reuse internal key + receive a standard TX in the same block.
		await SendToMempoolAsync(minerWallet, wallet, Money.Coins(1), firstInternalKeyScript, testDeadlineCts.Token);
		await SendToMempoolAsync(minerWallet, wallet, Money.Coins(1), walletExternalKeyScript, testDeadlineCts.Token);
		await node.GenerateBlockAsync(CancellationToken.None);

		await using var builder = new WalletBuilder(node);
		using var realWallet = await builder.CreateRealWalletBasedOnTestWalletAsync(wallet);
		await realWallet.WalletFilterProcessor.StartAsync(testDeadlineCts.Token);

		await realWallet.PerformSynchronizationAsync(SyncType.Turbo, testDeadlineCts.Token);
		Assert.Equal(3, realWallet.GetAllCoins().Count());

		await realWallet.PerformSynchronizationAsync(SyncType.NonTurbo, testDeadlineCts.Token);
		Assert.Equal(3, realWallet.GetAllCoins().Count());
	}

	private async Task SendToAsync(TestWallet spendingWallet, TestWallet receivingWallet, Money amount, IDestination destination, MockNode node, CancellationToken cancel)
	{
		await SendToMempoolAsync(spendingWallet, receivingWallet, amount, destination, cancel).ConfigureAwait(false);
		await node.GenerateBlockAsync(cancel).ConfigureAwait(false);
	}

	private async Task SendToMempoolAsync(TestWallet spendingWallet, TestWallet receivingWallet, Money amount, IDestination destination, CancellationToken cancel)
	{
		var tx = await spendingWallet.SendToAsync(amount, destination.ScriptPubKey, FeeRate.Zero, cancel).ConfigureAwait(false);
		receivingWallet.ScanTransaction(tx);
	}
}
