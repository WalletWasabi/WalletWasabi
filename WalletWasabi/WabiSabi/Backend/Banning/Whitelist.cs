using AsyncKeyedLock;
using NBitcoin;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.WabiSabi.Backend.Banning;

/// <summary>
/// Clean UTXOs are cached and saved here.
/// </summary>
public class Whitelist
{
	// Constructor used for testing.
	internal Whitelist(IEnumerable<Innocent> innocents, string filePath, WabiSabiConfig wabiSabiConfig)
	{
		Innocents = new ConcurrentDictionary<OutPoint, Innocent>(innocents.ToDictionary(k => k.Outpoint, v => v));
		if (!string.IsNullOrEmpty(filePath))
		{
			IoHelpers.EnsureFileExists(filePath);
		}
		WhitelistFilePath = filePath;
		WabiSabiConfig = wabiSabiConfig;
	}

	internal Guid ChangeId { get; set; } = Guid.Empty;
	private Guid LastSavedChangeId { get; set; } = Guid.Empty;
	private ConcurrentDictionary<OutPoint, Innocent> Innocents { get; }

	private string WhitelistFilePath { get; }
	private WabiSabiConfig WabiSabiConfig { get; }
	private AsyncNonKeyedLocker FileLock { get; } = new();

	public int CountInnocents()
	{
		return Innocents.Count;
	}

	public void Add(OutPoint outPoint)
		=> Save(new Innocent(outPoint, DateTimeOffset.UtcNow));

	private void Save(Innocent innocent)
	{
		if (Innocents.TryAdd(innocent.Outpoint, innocent))
		{
			ChangeId = Guid.NewGuid();
		}
	}

	public bool TryRelease(OutPoint utxo)
	{
		if (Innocents.TryRemove(utxo, out _))
		{
			ChangeId = Guid.NewGuid();
			return true;
		}

		return false;
	}

	public void RemoveAllExpired()
	{
		var allInnocentsToRemove = Innocents.Values.Where(innocent => innocent.TimeSpent > WabiSabiConfig.ReleaseFromWhitelistAfter);
		var removedCounter = allInnocentsToRemove.Select(innocent => TryRelease(innocent.Outpoint)).Count();
		if (removedCounter > 0)
		{
			Logger.LogInfo($"{removedCounter} utxo was expired and removed from Whitelist.");
		}
	}

	public bool TryGet(OutPoint utxo, [NotNullWhen(true)] out Innocent? innocent)
	{
		if (!Innocents.TryGetValue(utxo, out innocent))
		{
			return false;
		}

		if (innocent.TimeSpent > WabiSabiConfig.ReleaseFromWhitelistAfter)
		{
			TryRelease(innocent.Outpoint);
			Logger.LogInfo($"1 utxo was expired and removed from Whitelist.");
			return false;
		}

		return true;
	}

	public static async Task<Whitelist> CreateAndLoadFromFileAsync(string whitelistFilePath, WabiSabiConfig wabiSabiConfig, CancellationToken cancel)
	{
		var innocents = new List<Innocent>();
		try
		{
			if (File.Exists(whitelistFilePath))
			{
				var corruptedCounter = 0;
				bool shouldUpdate = false;
				var innocentFileContent = await File.ReadAllLinesAsync(whitelistFilePath, cancel).ConfigureAwait(false);
				foreach (var line in innocentFileContent)
				{
					if (Innocent.TryReadFromString(line, out Innocent? innocent))
					{
						innocents.Add(innocent);
					}
					else
					{
						corruptedCounter++;
						shouldUpdate = true;
					}
				}

				if (shouldUpdate)
				{
					Logger.LogWarning($"{corruptedCounter} corrupted innocents were found. Removing.");
					await File.WriteAllLinesAsync(whitelistFilePath, innocents.Select(innocent => innocent.ToString()), CancellationToken.None).ConfigureAwait(false);
				}
			}
			var whitelist = new Whitelist(innocents, whitelistFilePath, wabiSabiConfig);

			var numberOfInnocent = whitelist.CountInnocents();
			Logger.LogInfo($"{numberOfInnocent} UTXOs are found in whitelist.");

			return whitelist;
		}
		catch (Exception exc)
		{
			Logger.LogError("Reading from Whitelist failed with error:", exc);
			return new Whitelist(innocents, whitelistFilePath, wabiSabiConfig);
		}
	}

	public async Task<bool> WriteToFileIfChangedAsync()
	{
		if (ChangeId != LastSavedChangeId && !string.IsNullOrEmpty(WhitelistFilePath))
		{
			LastSavedChangeId = ChangeId;
			var toFile = Innocents.Values.Select(innocent => innocent.ToString());
			using (await FileLock.LockAsync().ConfigureAwait(false))
			{
				await File.WriteAllLinesAsync(WhitelistFilePath, toFile).ConfigureAwait(false);
				return true;
			}
		}
		return false;
	}
}
