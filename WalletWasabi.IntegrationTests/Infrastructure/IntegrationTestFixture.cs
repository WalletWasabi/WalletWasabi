using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using NBitcoin.RPC;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.IntegrationTests.BitcoinCore;

namespace WalletWasabi.IntegrationTests.Infrastructure;

/// <summary>
/// xUnit fixture that manages a shared Bitcoin Core instance for integration tests.
/// This fixture is created once per test collection and reused across all tests.
/// </summary>
public class IntegrationTestFixture : IDisposable
{
	private volatile bool _disposedValue = false;

	public IntegrationTestFixture()
	{
		BitcoinCoreNode = TestNodeBuilder
			.CreateAsync(callerFilePath: "IntegrationTests", callerMemberName: "BitcoinCoreData")
			.GetAwaiter()
			.GetResult();

		// Create a wallet for the Bitcoin Core node and get the wallet-specific RPC client
		const string walletName = "integration_test_wallet";
		RPCClient walletRpc = BitcoinCoreNode.RpcClient.CreateWalletAsync(walletName).GetAwaiter().GetResult();

		// Wrap the wallet-specific client in a CachedRpcClient for consistency
#pragma warning disable CA2000 // Dispose objects before losing scope - MemoryCache ownership transferred to CachedRpcClient
		WalletRpcClient = new CachedRpcClient(walletRpc, new MemoryCache(new MemoryCacheOptions()));
#pragma warning restore CA2000

		// Pre-mine some blocks to have mature coins available (using wallet-specific client)
		WalletRpcClient.GenerateAsync(110).GetAwaiter().GetResult();
	}

	/// <summary>
	/// The shared Bitcoin Core node for all integration tests.
	/// </summary>
	public CoreNode BitcoinCoreNode { get; }

	/// <summary>
	/// Wallet-specific RPC client for operations requiring wallet context.
	/// </summary>
	public IRPCClient WalletRpcClient { get; }

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				BitcoinCoreNode.TryStopAsync().GetAwaiter().GetResult();
			}

			_disposedValue = true;
		}
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}
}
