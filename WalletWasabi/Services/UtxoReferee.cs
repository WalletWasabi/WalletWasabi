using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models.ChaumianCoinJoin;

namespace WalletWasabi.Services
{
	public class UtxoReferee
	{
		/// <summary>
		/// Key: banned utxo, Value: severity level, time of ban, if it's only in noted status, which round it disrupted
		/// </summary>
		private ConcurrentDictionary<OutPoint, (int severity, DateTimeOffset timeOfBan, bool isNoted, long bannedForRound)> BannedUtxos { get; }

		public string BannedUtxosFilePath => Path.Combine(FolderPath, $"BannedUtxos{Network}.txt");

		public RPCClient RpcClient { get; }
		public Network Network { get; }

		public CcjRoundConfig RoundConfig { get; }

		public string FolderPath { get; }

		public UtxoReferee(Network network, string folderPath, RPCClient rpc, CcjRoundConfig roundConfig)
		{
			Network = Guard.NotNull(nameof(network), network);
			FolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(folderPath), folderPath, trim: true);
			RpcClient = Guard.NotNull(nameof(rpc), rpc);
			RoundConfig = Guard.NotNull(nameof(roundConfig), roundConfig);

			BannedUtxos = new ConcurrentDictionary<OutPoint, (int severity, DateTimeOffset timeOfBan, bool isNoted, long bannedForRound)>();

			Directory.CreateDirectory(FolderPath);

			if (File.Exists(BannedUtxosFilePath))
			{
				try
				{
					var toRemove = new List<string>(); // what's been confirmed
					string[] allLines = File.ReadAllLines(BannedUtxosFilePath);
					foreach (string line in allLines)
					{
						var bannedRecord = BannedRecordFromLine(line);

						GetTxOutResponse getTxOutResponse = RpcClient.GetTxOut(bannedRecord.utxo.Hash, (int)bannedRecord.utxo.N, includeMempool: true);

						// Check if inputs are unspent.
						if (getTxOutResponse is null)
						{
							toRemove.Add(line);
						}
						else
						{
							BannedUtxos.TryAdd(bannedRecord.utxo, (bannedRecord.severity, bannedRecord.timeOfBan, bannedRecord.isNoted, bannedRecord.bannedForRound));
						}
					}

					if (toRemove.Count != 0) // a little performance boost, often it'll be empty
					{
						var newAllLines = allLines.Where(x => !toRemove.Contains(x));
						File.WriteAllLines(BannedUtxosFilePath, newAllLines);
					}

					Logger.LogInfo<UtxoReferee>($"{allLines.Length} banned UTXOs are loaded from {BannedUtxosFilePath}.");
				}
				catch (Exception ex)
				{
					Logger.LogWarning<UtxoReferee>($"Banned UTXO file got corrupted. Deleting {BannedUtxosFilePath}. {ex.GetType()}: {ex.Message}");
					File.Delete(BannedUtxosFilePath);
				}
			}
			else
			{
				Logger.LogInfo<UtxoReferee>($"No banned UTXOs are loaded from {BannedUtxosFilePath}.");
			}
		}

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
				(int severity, DateTimeOffset timeOfBan, bool isNoted, long bannedForRound)? foundElem = null;
				if (BannedUtxos.TryGetValue(utxo, out (int severity, DateTimeOffset timeOfBan, bool isNoted, long bannedForRound) fe))
				{
					foundElem = fe;
					bool bannedForTheSameRound = foundElem.Value.bannedForRound == bannedForRound;
					if (bannedForTheSameRound && (!forceNoted || foundElem.Value.isNoted))
					{
						continue; // We would be simply duplicating this ban.
					}
				}

				var isNoted = true;
				if (forceNoted)
				{
					isNoted = true;
				}
				else
				{
					if (RoundConfig.DosNoteBeforeBan.Value)
					{
						if (foundElem.HasValue)
						{
							isNoted = false;
						}
					}
					else
					{
						isNoted = false;
					}
				}
				if (BannedUtxos.TryAdd(utxo, (severity, timeOfBan, isNoted, bannedForRound)))
				{
					string line = BannedRecordToLine(utxo, severity, timeOfBan, isNoted, bannedForRound);
					lines.Add(line);
				}
				else
				{
					var elem = BannedUtxos[utxo];
					if (elem.isNoted != isNoted || elem.bannedForRound != bannedForRound)
					{
						BannedUtxos[utxo] = (elem.severity, elem.timeOfBan, isNoted, bannedForRound);
						updated = true;
					}
				}

				Logger.LogInfo<UtxoReferee>($"UTXO {(isNoted ? "noted" : "banned")} with severity: {severity}. UTXO: {utxo.N}:{utxo.Hash} for disrupting Round {bannedForRound}.");
			}

			if (updated) // If at any time we set updated then we must update the whole thing.
			{
				var allLines = BannedUtxos.Select(x => $"{x.Value.timeOfBan.ToString(CultureInfo.InvariantCulture)}:{x.Value.severity}:{x.Key.N}:{x.Key.Hash}:{x.Value.isNoted}:{x.Value.bannedForRound}");
				await File.WriteAllLinesAsync(BannedUtxosFilePath, allLines);
			}
			else if (lines.Count != 0) // If we don't have to update the whole thing, we must check if we added a line and so only append.
			{
				await File.AppendAllLinesAsync(BannedUtxosFilePath, lines);
			}
		}

		public async Task UnbanAsync(OutPoint output)
		{
			if (BannedUtxos.TryRemove(output, out _))
			{
				IEnumerable<string> lines = BannedUtxos.Select(x => BannedRecordToLine(x.Key, x.Value.severity, x.Value.timeOfBan, x.Value.isNoted, x.Value.bannedForRound));
				await File.WriteAllLinesAsync(BannedUtxosFilePath, lines);
				Logger.LogInfo<UtxoReferee>($"UTXO unbanned: {output.N}:{output.Hash}.");
			}
		}

		public async Task<(int severity, TimeSpan bannedRemaining, DateTimeOffset timeOfBan, bool isNoted, long bannedForRound)?> TryGetBannedAsync(OutPoint outpoint, bool notedToo)
		{
			if (BannedUtxos.TryGetValue(outpoint, out (int severity, DateTimeOffset timeOfBan, bool isNoted, long bannedForRound) bannedElem))
			{
				int maxBan = (int)TimeSpan.FromHours(RoundConfig.DosDurationHours.Value).TotalMinutes;
				var bannedRemaining = DateTimeOffset.UtcNow - bannedElem.timeOfBan;
				int banLeftMinutes = maxBan - (int)bannedRemaining.TotalMinutes;
				if (banLeftMinutes > 0)
				{
					if (bannedElem.isNoted)
					{
						if (notedToo)
						{
							return (bannedElem.severity, bannedRemaining, bannedElem.timeOfBan, true, bannedElem.bannedForRound);
						}
						else
						{
							return null;
						}
					}
					else
					{
						return (bannedElem.severity, bannedRemaining, bannedElem.timeOfBan, false, bannedElem.bannedForRound);
					}
				}
				else
				{
					await UnbanAsync(outpoint);
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
				return BannedUtxos.Count(x => !x.Value.isNoted);
			}
		}

		public void Clear()
		{
			BannedUtxos.Clear();
			File.Delete(BannedUtxosFilePath);
		}

		internal static string BannedRecordToLine(OutPoint utxo, int severity, DateTimeOffset timeOfBan, bool isNoted, long bannedForRound)
		{
			return $"{timeOfBan.ToString("yyyy-MM-dd HH-mm-ss")}:{severity}:{utxo.N}:{utxo.Hash}:{isNoted}:{bannedForRound}";
		}

		internal static (OutPoint utxo, int severity, DateTimeOffset timeOfBan, bool isNoted, long bannedForRound) BannedRecordFromLine(string line)
		{
			var parts = line.Split(':');
			var isNoted = bool.Parse(parts[4]);
			var bannedForRound = long.Parse(parts[5]);
			var utxo = new OutPoint(new uint256(parts[3]), int.Parse(parts[2]));
			var severity = int.Parse(parts[1]);
			var timeParts = parts[0].Split('-', ' ').Select(x => int.Parse(x)).ToArray();
			var timeOfBan = new DateTimeOffset(timeParts[0], timeParts[1], timeParts[2], timeParts[3], timeParts[4], timeParts[5], TimeSpan.Zero);

			return (utxo, severity, timeOfBan, isNoted, bannedForRound);
		}
	}
}
