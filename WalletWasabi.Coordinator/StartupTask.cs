using NBitcoin;
using NBitcoin.RPC;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Logging;


namespace WalletWasabi.Coordinator;

public class StartupTask
{
	private IRPCClient RpcClient { get; }

	public StartupTask(IRPCClient rpc)
	{
		RpcClient = rpc;
	}

	public async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		Logger.LogInfo("Wasabi Coordinator");

		AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
		TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

		// Make sure RPC works.
		await AssertRpcNodeFullyInitializedAsync(cancellationToken).ConfigureAwait(false);
	}

	private async Task AssertRpcNodeFullyInitializedAsync(CancellationToken cancellationToken)
	{
		BlockchainInfo blockchainInfo;

		try
		{
			blockchainInfo = await RpcClient.GetBlockchainInfoAsync(cancellationToken);
		}
		catch (Exception)
		{
			Logger.LogError("Bitcoin Node is not running. Check RPC credentials.");
			throw;
		}

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
				using Key key = new();
				BitcoinAddress address = key.GetAddress(ScriptPubKeyType.Segwit, Network.RegTest);
				var generateBlocksResponse = await RpcClient.GenerateToAddressAsync(101, address, cancellationToken)
					?? throw new NotSupportedException($"Bitcoin Node cannot generate blocks on the {Network.RegTest}.");
				blockchainInfo = await RpcClient.GetBlockchainInfoAsync(cancellationToken);
				blocks = blockchainInfo.Blocks;
				if (blocks == 0)
				{
					throw new NotSupportedException($"{nameof(blocks)} == 0");
				}

				Logger.LogInfo($"Generated 101 block on {Network.RegTest}.");
			}

			Logger.LogDebug($"Number of blocks is {blocks}.");
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
