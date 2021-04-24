using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.CoinJoin.Coordinator.Rounds;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.CoinJoin.Coordinator.Banning
{
	public class UtxoReferee
	{
		public UtxoReferee(Network network, string folderPath, IRPCClient rpc, CoordinatorRoundConfig roundConfig)
		{
			Network = Guard.NotNull(nameof(network), network);
			FolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(folderPath), folderPath, trim: true);
			RpcClient = Guard.NotNull(nameof(rpc), rpc);
			RoundConfig = Guard.NotNull(nameof(roundConfig), roundConfig);

			BannedUtxos = new ConcurrentDictionary<OutPoint, BannedUtxo>();

			Directory.CreateDirectory(FolderPath);

			if (File.Exists(BannedUtxosFilePath))
			{
				try
				{
					var toRemove = new List<string>(); // what's been confirmed
					string[] allLines = File.ReadAllLines(BannedUtxosFilePath);
					foreach (string line in allLines)
					{
						var bannedRecord = BannedUtxo.FromString(line);

						var getTxOutResponse = RpcClient.GetTxOutAsync(bannedRecord.Utxo.Hash, (int)bannedRecord.Utxo.N, includeMempool: true).GetAwaiter().GetResult();

						// Check if inputs are unspent.
						if (getTxOutResponse is null)
						{
							toRemove.Add(line);
						}
						else
						{
							BannedUtxos.TryAdd(bannedRecord.Utxo, bannedRecord);
						}
					}

					if (toRemove.Count != 0) // a little performance boost, often it'll be empty
					{
						var newAllLines = allLines.Where(x => !toRemove.Contains(x));
						File.WriteAllLines(BannedUtxosFilePath, newAllLines);
					}

					Logger.LogInfo($"{allLines.Length} banned UTXOs are loaded from {BannedUtxosFilePath}.");
				}
				catch (Exception ex)
				{
					Logger.LogWarning($"Banned UTXO file got corrupted. Deleting {BannedUtxosFilePath}. {ex.GetType()}: {ex.Message}");
					File.Delete(BannedUtxosFilePath);
				}
			}
			else
			{
				Logger.LogInfo($"No banned UTXOs are loaded from {BannedUtxosFilePath}.");
			}
		}

		private ConcurrentDictionary<OutPoint, BannedUtxo> BannedUtxos { get; }

		public string BannedUtxosFilePath => Path.Combine(FolderPath, $"BannedUtxos{Network}.txt");

		public IRPCClient RpcClient { get; }
		public Network Network { get; }

		public CoordinatorRoundConfig RoundConfig { get; }

		public string FolderPath { get; }

		public async Task BanUtxosAsync(int severity, DateTimeOffset timeOfBan, bool forceNoted, long bannedForRound, params OutPoint[] toBan)
		{
			if (RoundConfig.DosSeverity == 0)
			{
				return;
			}

			var lines = new List<string>();
			var updated = false;
			foreach (var utxo in toBan)
			{
				BannedUtxo? foundElem = null;
				if (BannedUtxos.TryGetValue(utxo, out var fe))
				{
					foundElem = fe;
					bool bannedForTheSameRound = foundElem.BannedForRound == bannedForRound;
					if (bannedForTheSameRound && (!forceNoted || foundElem.IsNoted))
					{
						continue; // We would be simply duplicating this ban.
					}
				}

				var isNoted = true;
				if (!forceNoted)
				{
					if (RoundConfig.DosNoteBeforeBan)
					{
						if (foundElem is { })
						{
							isNoted = false;
						}
					}
					else
					{
						isNoted = false;
					}
				}

				var newElem = new BannedUtxo(utxo, severity, timeOfBan, isNoted, bannedForRound);
				if (BannedUtxos.TryAdd(newElem.Utxo, newElem))
				{
					lines.Add(newElem.ToString());
				}
				else
				{
					var elem = BannedUtxos[utxo];
					if (elem.IsNoted != isNoted || elem.BannedForRound != bannedForRound)
					{
						BannedUtxos[utxo] = new BannedUtxo(elem.Utxo, elem.Severity, elem.TimeOfBan, isNoted, bannedForRound);
						updated = true;
					}
				}

				Logger.LogInfo($"UTXO {(isNoted ? "noted" : "banned")} with severity: {severity}. UTXO: {utxo.N}:{utxo.Hash} for disrupting Round {bannedForRound}.");
			}

			if (updated) // If at any time we set updated then we must update the whole thing.
			{
				var allLines = BannedUtxos.Select(x => x.Value.ToString());
				await File.WriteAllLinesAsync(BannedUtxosFilePath, allLines).ConfigureAwait(false);
			}
			else if (lines.Count != 0) // If we do not have to update the whole thing, we must check if we added a line and so only append.
			{
				await File.AppendAllLinesAsync(BannedUtxosFilePath, lines).ConfigureAwait(false);
			}
		}

		public async Task UnbanAsync(OutPoint output)
		{
			if (BannedUtxos.TryRemove(output, out _))
			{
				IEnumerable<string> lines = BannedUtxos.Select(x => x.Value.ToString());
				await File.WriteAllLinesAsync(BannedUtxosFilePath, lines).ConfigureAwait(false);
				Logger.LogInfo($"UTXO unbanned: {output.N}:{output.Hash}.");
			}
		}

		public async Task<BannedUtxo?> TryGetBannedAsync(OutPoint outpoint, bool notedToo)
		{
			if (BannedUtxos.TryGetValue(outpoint, out var bannedElem))
			{
				int maxBan = (int)TimeSpan.FromHours(RoundConfig.DosDurationHours).TotalMinutes;
				int banLeftMinutes = maxBan - (int)bannedElem.BannedRemaining.TotalMinutes;
				if (banLeftMinutes > 0)
				{
					if (bannedElem.IsNoted)
					{
						if (notedToo)
						{
							return new BannedUtxo(bannedElem.Utxo, bannedElem.Severity, bannedElem.TimeOfBan, true, bannedElem.BannedForRound);
						}
						else
						{
							return null;
						}
					}
					else
					{
						return new BannedUtxo(bannedElem.Utxo, bannedElem.Severity, bannedElem.TimeOfBan, false, bannedElem.BannedForRound);
					}
				}
				else
				{
					await UnbanAsync(outpoint).ConfigureAwait(false);
				}
			}
			return null;
		}

		public int CountBanned(bool notedToo)
		{
			if (notedToo)
			{
				return BannedUtxos.Count;
			}
			else
			{
				return BannedUtxos.Count(x => !x.Value.IsNoted);
			}
		}

		public void Clear()
		{
			BannedUtxos.Clear();
			File.Delete(BannedUtxosFilePath);
		}
	}
}
