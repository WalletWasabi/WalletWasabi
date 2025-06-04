using System.IO;
using NBitcoin;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Indexer;

public class StartupTask
{
	private IndexBuilderService IndexBuilderService { get; }
	private IRPCClient RpcClient { get; }

	public StartupTask(Config config, IRPCClient rpc, IndexBuilderService indexBuilderService)
	{
		IndexBuilderService = indexBuilderService;
		RpcClient = rpc;
	}

	public async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		Logger.LogInfo("Wasabi Indexer");

		AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
		TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

		// Make sure RPC works.
		var assertRpcNodeTask = AssertRpcNodeFullyInitializedAsync(cancellationToken);
		var startIndexingService = IndexBuilderService.StartAsync(cancellationToken);
		await Task.WhenAll(assertRpcNodeTask, startIndexingService).ConfigureAwait(false);

		Logger.LogInfo($"{nameof(IndexBuilderService)} is successfully initialized and started synchronization.");
	}

	private async Task AssertRpcNodeFullyInitializedAsync(CancellationToken cancellationToken)
	{
		try
		{
			var blockchainInfo = await RpcClient.GetBlockchainInfoAsync(cancellationToken);

			var blocks = blockchainInfo.Blocks;
			if (blocks == 0 && RpcClient.Network != Network.RegTest)
			{
				throw new NotSupportedException($"{nameof(blocks)} == 0");
			}

			var headers = blockchainInfo.Headers;
			if (headers == 0 && RpcClient.Network != Network.RegTest)
			{
				throw new NotSupportedException($"{nameof(headers)} == 0");
			}

			if (blocks != headers)
			{
				throw new NotSupportedException("Bitcoin Node is not fully synchronized.");
			}

			Logger.LogInfo("Bitcoin Node is fully synchronized.");

			if (RpcClient.Network == Network.RegTest) // Make sure there's at least 101 block, if not generate it
			{
				if (blocks < 101)
				{
					using var key = new Key();
					var address = key.GetAddress(ScriptPubKeyType.TaprootBIP86, RpcClient.Network);
					var generateBlocksResponse = await RpcClient.GenerateToAddressAsync(101, address, cancellationToken);
					if (generateBlocksResponse is null)
					{
						throw new NotSupportedException($"Bitcoin Node cannot generate blocks on the {Network.RegTest}.");
					}

					blockchainInfo = await RpcClient.GetBlockchainInfoAsync(cancellationToken);
					blocks = blockchainInfo.Blocks;
					if (blocks == 0)
					{
						throw new NotSupportedException($"{nameof(blocks)} == 0");
					}
					Logger.LogInfo($"Generated 101 block on {Network.RegTest}. Number of blocks {blocks}.");
				}
			}
		}
		catch (WebException)
		{
			Logger.LogError($"Bitcoin Node is not running, or incorrect RPC credentials, or network is given in the config file.");
			throw;
		}
	}

	private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
	{
		Logger.LogWarning(e.Exception, "Unexpected unobserved task exception.");
	}

	private void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
	{
		if (e.ExceptionObject is Exception ex)
		{
			Logger.LogWarning(ex, "Unhandled exception.");
		}
	}
}
