using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NBitcoin;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;

namespace WalletWasabi.WabiSabi.Backend.DoSPrevention;

public class Warden : BackgroundService
{
	public Warden(string prisonFilePath, ICoinJoinIdStore coinjoinIdStore, WabiSabiConfig config)
	{
		PrisonFilePath = prisonFilePath;
		OffendersToSaveChannel = Channel.CreateUnbounded<Offender>();

		var dosConfig = new DoSConfiguration(
			SeverityInBitcoinsPerHour: config.DoSSeverity.ToDecimal(MoneyUnit.BTC),
			MinTimeForFailedToVerify: config.DoSMinTimeForFailedToVerify,
			MinTimeForCheating: config.DoSMinTimeForCheating,
			PenaltyFactorForDisruptingConfirmation: (decimal) config.DoSPenaltyFactorForDisruptingConfirmation,
			PenaltyFactorForDisruptingSigning: (decimal) config.DoSPenaltyFactorForDisruptingSigning,
			PenaltyFactorForDisruptingByDoubleSpending: (decimal) config.DoSPenaltyFactorForDisruptingByDoubleSpending,
			MinTimeInPrison: config.DoSMinTimeInPrison);
		Prison = DeserializePrison(PrisonFilePath, dosConfig, coinjoinIdStore, OffendersToSaveChannel.Writer);
	}

	public Prison Prison { get; }

	public string PrisonFilePath { get; }

	private Channel<Offender> OffendersToSaveChannel { get; }

	private static Prison DeserializePrison(
		string prisonFilePath,
		DoSConfiguration config,
		ICoinJoinIdStore coinjoinIdStore,
		ChannelWriter<Offender> channelWriter)
	{
		IoHelpers.EnsureContainingDirectoryExists(prisonFilePath);
		var offenders = new List<Offender>();
		if (File.Exists(prisonFilePath))
		{
			try
			{
				foreach (var offender in File.ReadAllLines(prisonFilePath).Select(Offender.FromStringLine))
				{
					offenders.Add(offender);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				Logger.LogWarning($"Deleting {prisonFilePath}");
				File.Delete(prisonFilePath);
			}
		}

		return new Prison(config, coinjoinIdStore, offenders, channelWriter);
	}

	protected override async Task ExecuteAsync(CancellationToken cancel)
	{
		try
		{
			while (!cancel.IsCancellationRequested)
			{
				await foreach (var inmate in OffendersToSaveChannel.Reader.ReadAllAsync(cancel).ConfigureAwait(false))
				{
					var lines = Enumerable.Repeat(inmate.ToStringLine(), 1);
					await File.AppendAllLinesAsync(PrisonFilePath, lines, CancellationToken.None).ConfigureAwait(false);
				}
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			throw;
		}
	}

	public override Task StopAsync(CancellationToken cancellationToken)
	{
		OffendersToSaveChannel.Writer.Complete();
		return base.StopAsync(cancellationToken);
	}

	public void BanDescendant(object? sender, Block block)
	{
		var now = DateTimeOffset.UtcNow;

		bool IsInputBanned(TxIn input) => Prison.IsBanned(input.PrevOut, now);
		OutPoint[] BannedInputs(Transaction tx) => tx.Inputs.Where(IsInputBanned).Select(x => x.PrevOut).ToArray();

		var outpointsToBan = block.Transactions
			.Select(tx => (Tx: tx, BannedInputs: BannedInputs(tx)))
			.Where(x => x.BannedInputs.Any())
			.SelectMany(x => x.Tx.Outputs.Select((_, i) => (new OutPoint(x.Tx, i), x.BannedInputs)));

		foreach (var (outpoint, ancestors) in outpointsToBan)
		{
			Prison.InheritPunishment(outpoint, ancestors);
		}
	}
}
