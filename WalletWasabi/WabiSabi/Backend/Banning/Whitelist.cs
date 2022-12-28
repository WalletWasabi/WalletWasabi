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
using WalletWasabi.WabiSabi.Backend.Models;

namespace WalletWasabi.WabiSabi.Backend.Banning;

/// <summary>
/// Clean UTXOs are cached and saved here.
/// </summary>
public class Whitelist
{
	internal Whitelist() : this(Enumerable.Empty<Innocent>(), string.Empty)
	{
	}

	private Whitelist(IEnumerable<Innocent> innocents, string filePath)
	{
		Innocents = new ConcurrentDictionary<OutPoint, Innocent>(innocents.ToDictionary(k => k.Outpoint, v => v));
		if (!string.IsNullOrEmpty(filePath))
		{
			IoHelpers.EnsureFileExists(filePath);
		}
		WhitelistFilePath = filePath;
	}

	public Guid ChangeId { get; private set; } = Guid.NewGuid();
	private ConcurrentDictionary<OutPoint, Innocent> Innocents { get; }

	private string WhitelistFilePath { get; }

	public int CountInnocents()
	{
		return Innocents.Count;
	}

	public void Add(Alice alice)
		=> Save(new Innocent(alice.Coin.Outpoint, DateTimeOffset.UtcNow));

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

	public void RemoveAllExpired(TimeSpan releaseTime)
	{
		var allInnocentsToRemove = GetInnocents().Where(innocent => innocent.TimeSpent > releaseTime);
		var removedCounter = allInnocentsToRemove.Select(innocent => TryRelease(innocent.Outpoint)).Count();
		if (removedCounter > 0)
		{
			Logger.LogInfo($"{removedCounter} utxo was expired and removed from Whitelist.");
		}
	}

	public bool TryGet(OutPoint utxo, [NotNullWhen(true)] out Innocent? innocent)
	{
		return Innocents.TryGetValue(utxo, out innocent);
	}

	public IEnumerable<Innocent> GetInnocents()
	{
		return Innocents.Values;
	}

	public static async Task<Whitelist> CreateAndLoadFromFileAsync(string whitelistFilePath, CancellationToken cancel)
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
			var whitelist = new Whitelist(innocents, whitelistFilePath);

			var numberOfInnocent = whitelist.CountInnocents();
			Logger.LogInfo($"{numberOfInnocent} UTXOs are found in whitelist.");

			return whitelist;
		}
		catch (Exception exc)
		{
			Logger.LogError("Reading from Whitelist failed with error:", exc);
			return new Whitelist(innocents, whitelistFilePath);
		}
	}

	public void WriteToFile()
	{
		if (!string.IsNullOrEmpty(WhitelistFilePath))
		{
			var toFile = GetInnocents().Select(innocent => innocent.ToString());
			File.WriteAllLines(WhitelistFilePath, toFile);
		}
	}
}
