using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Coordinator.DoSPrevention;

namespace WalletWasabi.Coordinator.WabiSabi.DosPrevention;

public class Warden : BackgroundService
{
	public Warden(string prisonFilePath, WabiSabiConfig config)
	{
		_prisonFilePath = prisonFilePath;
		_config = config;
		_offendersToSaveChannel = Channel.CreateUnbounded<Offender>();

		Prison = DeserializePrison(_prisonFilePath, _offendersToSaveChannel.Writer);
	}

	public Prison Prison { get; }

	private readonly string _prisonFilePath;
	private readonly WabiSabiConfig _config;

	private readonly Channel<Offender> _offendersToSaveChannel;

	private static Prison DeserializePrison( string prisonFilePath, ChannelWriter<Offender> channelWriter)
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

		return new Prison(offenders, channelWriter);
	}

	protected override async Task ExecuteAsync(CancellationToken cancel)
	{
		try
		{
			while (!cancel.IsCancellationRequested)
			{
				await foreach (var inmate in _offendersToSaveChannel.Reader.ReadAllAsync(cancel).ConfigureAwait(false))
				{
					await File.AppendAllLinesAsync(_prisonFilePath, [inmate.ToStringLine()], CancellationToken.None).ConfigureAwait(false);
				}
			}
		}
		catch (OperationCanceledException)
		{
			Logger.LogInfo("Warden was requested to stop.");
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			throw;
		}
	}

	public override Task StopAsync(CancellationToken cancellationToken)
	{
		_offendersToSaveChannel.Writer.Complete();
		return base.StopAsync(cancellationToken);
	}
}
