using Microsoft.Extensions.Hosting;
using NBitcoin;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Coordinator;
using WalletWasabi.WabiSabi.Coordinator.DoSPrevention;

namespace WalletWasabi.Coordinator.Services;

public class CoinBanningService : BackgroundService
{
	public const string ZmqHost = "127.0.0.1";
	public const int ZmqPort = 28332;

	public CoinBanningService(WabiSabiConfig config, IRPCClient rpcClient, Prison prison)
		: this(config, rpcClient, prison, Channel.CreateUnbounded<ZmqEvent>(), zmqSubscriber: null)
	{
	}

	/// <remarks>Constructor for tests.</remarks>
	internal CoinBanningService(WabiSabiConfig config, IRPCClient rpcClient, Prison prison, Channel<ZmqEvent> msgReader, IHostedService? zmqSubscriber)
	{
		Config = config;
		RpcClient = rpcClient;
		Prison = prison;
		MsgReader = msgReader;
		ZmqSubscriber = zmqSubscriber ?? new ZmtpBlockHashSubscriber(ZmqHost, ZmqPort, msgReader.Writer);
	}

	private WabiSabiConfig Config { get; }
	private IRPCClient RpcClient { get; }
	private Prison Prison { get; }
	private ChannelReader<ZmqEvent> MsgReader { get; }
	private IHostedService ZmqSubscriber { get; }

	public override async Task StartAsync(CancellationToken cancellationToken)
	{
		Logger.LogDebug("Starting the service.");

		await ZmqSubscriber.StartAsync(cancellationToken).ConfigureAwait(false);
		await base.StartAsync(cancellationToken).ConfigureAwait(false);

		Logger.LogInfo("The service started.");
	}

	protected override async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var ev in MsgReader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
			{
				// The first event is that we got connected to a ZMQ server.
				if (ev.Hash is not { } bestBlockHash)
				{
					continue;
				}

				try
				{
					Logger.LogDebug($"Received new block hash '{bestBlockHash}'.");
					await HandleNewBlockAsync(bestBlockHash, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					Logger.LogError($"Failed to handle block '{bestBlockHash}'.");
					Logger.LogError(ex);
				}
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Logger.LogError("The service stopped with an error. It's safe to continue but descendant banning will not work as expected.");
			Logger.LogError(ex);
		}
	}

	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		Logger.LogDebug("Stopping the service.");

		await ZmqSubscriber.StopAsync(cancellationToken).ConfigureAwait(false);
		await base.StopAsync(cancellationToken).ConfigureAwait(false);

		Logger.LogInfo("The service stopped.");
	}

	private async Task HandleNewBlockAsync(uint256 bestBlockHash, CancellationToken cancellationToken)
	{
		var verboseBlock = await RpcClient.GetVerboseBlockAsync(bestBlockHash, cancellationToken).ConfigureAwait(false);
		var block = verboseBlock.Block!;

		var dosConfig = Config.GetDoSConfiguration();
		var now = DateTimeOffset.UtcNow;

		// Block transactions are sorted in topological order (with respect to spending of UTXOs). Therefore, apply punishments as you go, not after the loop.
		foreach (var tx in block.Transactions)
		{
			if (tx.IsCoinBase)
			{
				continue;
			}

			var bannedPrevOuts = tx.Inputs
				.Select(input => input.PrevOut)
				.Where(prevOut => Prison.IsBanned(prevOut, dosConfig, now))
				.ToArray();

			if (bannedPrevOuts.Length == 0)
			{
				continue;
			}

			for (int i = 0; i < tx.Outputs.Count; i++)
			{
				var outpoint = new OutPoint(tx, i);
				Logger.LogInfo($"Punish outpoint '{outpoint}' with ancestors: {string.Join(", ", bannedPrevOuts)}");
				Prison.InheritPunishment(outpoint, bannedPrevOuts);
			}
		}
	}

	public override void Dispose()
	{
		if (ZmqSubscriber is IDisposable disposable)
		{
			disposable.Dispose();
		}

		base.Dispose();
	}
}
