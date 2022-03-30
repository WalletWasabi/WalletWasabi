using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Logging;

namespace WalletWasabi.WabiSabi.Backend.Banning;

/// <summary>
/// Serializes and releases the prison population periodically.
/// </summary>
public class Warden : PeriodicRunner
{
	/// <param name="period">How often to serialize and release inmates.</param>
	public Warden(TimeSpan period, string prisonFilePath, WabiSabiConfig config) : base(period)
	{
		PrisonFilePath = prisonFilePath;
		Config = config;
		Prison = DeserializePrison(PrisonFilePath);
		LastKnownChange = Prison.ChangeId;
	}

	public Prison Prison { get; }
	public Guid LastKnownChange { get; private set; }

	public string PrisonFilePath { get; }
	public WabiSabiConfig Config { get; }

	private static Prison DeserializePrison(string prisonFilePath)
	{
		IoHelpers.EnsureContainingDirectoryExists(prisonFilePath);
		var inmates = new List<Inmate>();
		if (File.Exists(prisonFilePath))
		{
			try
			{
				foreach (var inmate in File.ReadAllLines(prisonFilePath).Select(Inmate.FromString))
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

		var prison = new Prison(inmates);

		var (noted, banned) = prison.CountInmates();
		if (noted > 0)
		{
			Logger.LogInfo($"{noted} noted UTXOs are found in prison.");
		}

		if (banned > 0)
		{
			Logger.LogInfo($"{banned} banned UTXOs are found in prison.");
		}

		return prison;
	}

	public async Task SerializePrisonAsync()
	{
		IoHelpers.EnsureContainingDirectoryExists(PrisonFilePath);
		await File.WriteAllLinesAsync(PrisonFilePath, Prison.GetInmates().Select(x => x.ToString())).ConfigureAwait(false);
	}

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		var count = Prison.ReleaseEligibleInmates(Config.ReleaseUtxoFromPrisonAfter, Config.ReleaseUtxoFromPrisonAfterLongBan).Count();

		if (count > 0)
		{
			Logger.LogInfo($"{count} UTXOs are released from prison.");
		}

		// If something changed, send prison to file.
		if (LastKnownChange != Prison.ChangeId)
		{
			await SerializePrisonAsync().ConfigureAwait(false);
			LastKnownChange = Prison.ChangeId;
		}
	}
}
