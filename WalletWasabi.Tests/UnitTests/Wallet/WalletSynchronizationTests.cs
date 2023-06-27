using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using NBitcoin;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Wallet;

public class WalletSynchronizationTests
{
	/// <summary>
	/// Receive on an internal key then spend (-> Key in subset SyncType.NonTurbo) then receive again.
	/// Verifies that the wallet won't find the last TX during Turbo sync but will find it during NonTurbo.
	/// </summary>
	[Fact]
	public async Task InternalAddressReuseNoBlockOverlapTestAsync()
	{
		var node = await MockNode.CreateNodeAsync();
		var minerWallet = node.Wallet;
		var wallet = new TestWallet("wallet", node.Rpc);

		var minerFirstKeyScript = minerWallet.GetNextDestination();
		var firstInternalKeyScript = wallet.GetNextInternalDestination();

		// First receive.
		await SendToAsync(minerWallet, wallet, Money.Coins(1), firstInternalKeyScript, node);

		// Send the money away.
		await SendToAsync(wallet, minerWallet, Money.Coins(1), minerFirstKeyScript, node);

		// Address re-use.
		await SendToAsync(minerWallet, wallet, Money.Coins(2), firstInternalKeyScript, node);

		await using var builder = new WalletBuilder(node);
		using var realWallet = await builder.CreateRealWalletBasedOnTestWalletAsync(wallet);
		var coins = (CoinsRegistry)realWallet.Coins;

		await realWallet.PerformWalletSynchronizationAsync(SyncType.Turbo, CancellationToken.None);
		Assert.Single(coins.AsAllCoinsView());

		await realWallet.PerformWalletSynchronizationAsync(SyncType.NonTurbo, CancellationToken.None);
		Assert.Equal(2, coins.AsAllCoinsView().Count());
	}

	/// <summary>
	/// Receive on an internal key then spend (-> Key in subset SyncType.NonTurbo) then receive again and spend to an external key in a different block.
	/// Verifies that the wallet will process the spend correctly when it doesn't have the coins in its CoinsRegistry at the time of spending.
	/// </summary>
	[Fact]
	public async Task InternalAddressReuseThenSpendOnExternalKeyTestAsync()
	{
		var node = await MockNode.CreateNodeAsync();
		var minerWallet = node.Wallet;
		var wallet = new TestWallet("wallet", node.Rpc);

		var minerFirstKeyScript = minerWallet.GetNextDestination();
		var firstInternalKeyScript = wallet.GetNextInternalDestination();
		var walletExternalKeyScript = wallet.GetNextDestination();

		// First receive.
		await SendToAsync(minerWallet, wallet, Money.Coins(1), firstInternalKeyScript, node);

		// Send the money away.
		await SendToAsync(wallet, minerWallet, Money.Coins(1), minerFirstKeyScript, node);

		// Address re-use.
		await SendToAsync(minerWallet, wallet, Money.Coins(2), firstInternalKeyScript, node);

		// Self spend the coins to an external key.
		await SendToAsync(wallet, wallet, Money.Coins(2), walletExternalKeyScript, node);

		await using var builder = new WalletBuilder(node);
		using var realWallet = await builder.CreateRealWalletBasedOnTestWalletAsync(wallet);
		var coins = (CoinsRegistry)realWallet.Coins;

		await realWallet.PerformWalletSynchronizationAsync(SyncType.Turbo, CancellationToken.None);
		Assert.Single(coins.Available());

		await realWallet.PerformWalletSynchronizationAsync(SyncType.NonTurbo, CancellationToken.None);
		Assert.Single(coins.Available());
	}

	/// <summary>
	/// Reuse 2 internal keys then send all funds away, then receive on first one, send to second one, then send on an external key.
	/// This aims to make sure that the CoinsRegistry will catch all the history.
	/// </summary>
	[Fact]
	public async Task InternalAddressReuseChainThenSpendOnExternalKeyTestAsync()
	{
		var node = await MockNode.CreateNodeAsync();
		var minerWallet = node.Wallet;
		var wallet = new TestWallet("wallet", node.Rpc);

		var minerFirstKeyScript = minerWallet.GetNextDestination();
		var firstInternalKeyScript = wallet.GetNextInternalDestination();
		var secondInternalKeyScript = wallet.GetNextInternalDestination();
		var walletExternalKeyScript = wallet.GetNextDestination();

		// First address reuse and send money away
		await SendToAsync(minerWallet, wallet, Money.Coins(1), firstInternalKeyScript, node);
		await SendToAsync(wallet, minerWallet, Money.Coins(1), minerFirstKeyScript, node);
		await SendToAsync(minerWallet, wallet, Money.Coins(2), firstInternalKeyScript, node);
		await SendToAsync(wallet, minerWallet, Money.Coins(2), minerFirstKeyScript, node);

		// Second address reuse and send money away
		await SendToAsync(minerWallet, wallet, Money.Coins(1), secondInternalKeyScript, node);
		await SendToAsync(wallet, minerWallet, Money.Coins(1), minerFirstKeyScript, node);
		await SendToAsync(minerWallet, wallet, Money.Coins(2), secondInternalKeyScript, node);
		await SendToAsync(wallet, minerWallet, Money.Coins(2), minerFirstKeyScript, node);

		// Receive again on first internal key
		await SendToAsync(minerWallet, wallet, Money.Coins(3), firstInternalKeyScript, node);

		// Self spend the coins to second internal key
		await SendToAsync(wallet, wallet, Money.Coins(3), secondInternalKeyScript, node);

		// Self spend the coins to an external key
		await SendToAsync(wallet, wallet, Money.Coins(3), walletExternalKeyScript, node);

		await using var builder = new WalletBuilder(node);
		using var realWallet = await builder.CreateRealWalletBasedOnTestWalletAsync(wallet);
		var coins = (CoinsRegistry)realWallet.Coins;

		await realWallet.PerformWalletSynchronizationAsync(SyncType.Turbo, CancellationToken.None);
		Assert.Single(coins.Available());

		await realWallet.PerformWalletSynchronizationAsync(SyncType.NonTurbo, CancellationToken.None);
		Assert.Equal(7, coins.AsAllCoinsView().Count());
	}

	/// <summary>
	/// Receive on an internal key then spend (-> Key in subset SyncType.NonTurbo) then receive again but in the same block receive on an external key.
	/// Verifies that the wallet will find the TX reusing internal key twice (once in Turbo because of the TX on ext key in the same block and again in NonTurbo), but will process it without issues.
	/// </summary>
	[Fact]
	public async Task InternalAddressReuseWithBlockOverlapTestAsync()
	{
		var node = await MockNode.CreateNodeAsync();
		var minerWallet = node.Wallet;
		var wallet = new TestWallet("wallet", node.Rpc);

		var minerFirstKeyScript = minerWallet.GetNextDestination();
		var firstInternalKeyScript = wallet.GetNextInternalDestination();
		var walletExternalKeyScript = wallet.GetNextDestination();

		// First receive.
		await SendToAsync(minerWallet, wallet, Money.Coins(1), firstInternalKeyScript, node);

		// Send the money away.
		await SendToAsync(wallet, minerWallet, Money.Coins(1), minerFirstKeyScript, node);

		// Reuse internal key + receive a standard TX in the same block.
		await SendToMempoolAsync(minerWallet, wallet, Money.Coins(1), firstInternalKeyScript);
		await SendToMempoolAsync(minerWallet, wallet, Money.Coins(1), walletExternalKeyScript);
		await node.GenerateBlockAsync(CancellationToken.None);

		await using var builder = new WalletBuilder(node);
		using var realWallet = await builder.CreateRealWalletBasedOnTestWalletAsync(wallet);
		var coins = (CoinsRegistry)realWallet.Coins;

		await realWallet.PerformWalletSynchronizationAsync(SyncType.Turbo, CancellationToken.None);
		Assert.Equal(3, coins.AsAllCoinsView().Count());

		await realWallet.PerformWalletSynchronizationAsync(SyncType.NonTurbo, CancellationToken.None);
		Assert.Equal(3, coins.AsAllCoinsView().Count());
	}

	private async Task SendToAsync(TestWallet spendingWallet, TestWallet receivingWallet, Money amount, IDestination destination, MockNode node, CancellationToken cancel = default)
	{
		await SendToMempoolAsync(spendingWallet, receivingWallet, amount, destination, cancel);
		await node.GenerateBlockAsync(cancel);
	}

	private async Task SendToMempoolAsync(TestWallet spendingWallet, TestWallet receivingWallet, Money amount, IDestination destination, CancellationToken cancel = default)
	{
		var tx = await spendingWallet.SendToAsync(amount, destination.ScriptPubKey, FeeRate.Zero, cancel);
		receivingWallet.ScanTransaction(tx);
	}
}
