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
			Severity: config.DoSSeverity.ToDecimal(MoneyUnit.BTC),
			MinTimeForFailedToVerify: config.DoSMinTimeForFailedToVerify,
			MinTimeForCheating: config.DoSMinTimeForCheating,
			PenaltyFactorForDisruptingConfirmation: (decimal) config.DoSPenaltyFactorForDisruptingConfirmation,
			PenaltyFactorForDisruptingSigning: (decimal) config.DoSPenaltyFactorForDisruptingSigning,
			PenaltyFactorForDisruptingByDoubleSpending: (decimal) config.DoSPenaltyFactorForDisruptingByDoubleSpending,
			MinimumTimeInPrison: config.DoSMinimumHoursInPrison);
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
		var inmates = new List<Offender>();
		if (File.Exists(prisonFilePath))
		{
			try
			{
				foreach (var inmate in File.ReadAllLines(prisonFilePath).Select(Offender.FromStringLine))
				{
					inmates.Add(inmate);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				Logger.LogWarning($"Deleting {prisonFilePath}");
				File.Delete(prisonFilePath);
			}
		}

		return new Prison(config, coinjoinIdStore, inmates, channelWriter);
	}

	protected override async Task ExecuteAsync(CancellationToken cancel)
	{
		while (!cancel.IsCancellationRequested)
		{
			await foreach (var inmate in OffendersToSaveChannel.Reader.ReadAllAsync(cancel))
			{
				var lines = Enumerable.Repeat(inmate.ToStringLine(), 1);
				await File.AppendAllLinesAsync(PrisonFilePath, lines, CancellationToken.None).ConfigureAwait(false);
			}
		}
	}
}
