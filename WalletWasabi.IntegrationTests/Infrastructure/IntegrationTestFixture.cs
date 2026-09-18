using Microsoft.Extensions.Caching.Memory;
using System;
using System.Threading.Tasks;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.IntegrationTests.BitcoinCore;
using Xunit;

namespace WalletWasabi.IntegrationTests.Infrastructure;

/// <summary>
/// xUnit fixture that manages a shared Bitcoin Core instance for integration tests.
/// This fixture is created once per test collection and reused across all tests.
/// </summary>
public class IntegrationTestFixture : IAsyncLifetime, IAsyncDisposable
{
	private volatile bool _disposedValue = false;

	/// <summary>The shared Bitcoin Core node for all integration tests.</summary>
	public CoreNode BitcoinCoreNode { get; private set; } = null!;

	/// <summary>Wallet-specific RPC client for operations requiring wallet context.</summary>
	public IRPCClient WalletRpcClient { get; private set; } = null!;

	public async ValueTask InitializeAsync()
	{
		BitcoinCoreNode = await TestNodeBuilder.CreateAsync(callerFilePath: "IntegrationTests", callerMemberName: "BitcoinCoreData");

		// Create a wallet for the Bitcoin Core node and get the wallet-specific RPC client
		var walletRpc = await BitcoinCoreNode.RpcClient.CreateWalletAsync(walletNameOrPath: "integration_test_wallet");

		// Wrap the wallet-specific client in a CachedRpcClient for consistency
#pragma warning disable CA2000 // Dispose objects before losing scope - MemoryCache ownership transferred to CachedRpcClient
		WalletRpcClient = new CachedRpcClient(walletRpc, new MemoryCache(new MemoryCacheOptions()));
#pragma warning restore CA2000

		// Pre-mine some blocks to have mature coins available (using wallet-specific client)
		await WalletRpcClient.GenerateAsync(110);
	}

	public async ValueTask DisposeAsync()
	{
		if (!_disposedValue)
		{
			await BitcoinCoreNode.TryStopAsync();
			_disposedValue = true;
		}
	}
}
