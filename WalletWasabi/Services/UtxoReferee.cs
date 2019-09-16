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
using WalletWasabi.Models;
using WalletWasabi.Models.ChaumianCoinJoin;

namespace WalletWasabi.Services
{
	public class UtxoReferee
	{
		private ConcurrentDictionary<OutPoint, BannedUtxoRecord> BannedUtxos { get; }

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

			BannedUtxos = new ConcurrentDictionary<OutPoint, BannedUtxoRecord>();

			Directory.CreateDirectory(FolderPath);

			if (File.Exists(BannedUtxosFilePath))
			{
				try
				{
					var toRemove = new List<string>(); // what's been confirmed
					string[] allLines = File.ReadAllLines(BannedUtxosFilePath);
					foreach (string line in allLines)
					{
						var bannedRecord = BannedUtxoRecord.FromString(line);

						GetTxOutResponse getTxOutResponse = RpcClient.GetTxOut(bannedRecord.Utxo.Hash, (int)bannedRecord.Utxo.N, includeMempool: true);

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
				BannedUtxoRecord foundElem = null;
				if (BannedUtxos.TryGetValue(utxo, out BannedUtxoRecord fe))
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
						if (foundElem != null)
						{
							isNoted = false;
						}
					}
					else
					{
						isNoted = false;
					}
				}

				var newElem = new BannedUtxoRecord(utxo, severity, timeOfBan, isNoted, bannedForRound);
				if (BannedUtxos.TryAdd(newElem.Utxo, newElem))
				{
					string line = newElem.ToString();
					lines.Add(line);
				}
				else
				{
					var elem = BannedUtxos[utxo];
					if (elem.IsNoted != isNoted || elem.BannedForRound != bannedForRound)
					{
						BannedUtxos[utxo] = new BannedUtxoRecord(elem.Utxo, elem.Severity, elem.TimeOfBan, isNoted, bannedForRound);
						updated = true;
					}
				}

				Logger.LogInfo($"UTXO {(isNoted ? "noted" : "banned")} with severity: {severity}. UTXO: {utxo.N}:{utxo.Hash} for disrupting Round {bannedForRound}.");
			}

			if (updated) // If at any time we set updated then we must update the whole thing.
			{
				var allLines = BannedUtxos.Select(x => $"{x.Value.TimeOfBan.ToString(CultureInfo.InvariantCulture)}:{x.Value.Severity}:{x.Key.N}:{x.Key.Hash}:{x.Value.IsNoted}:{x.Value.BannedForRound}");
				await File.WriteAllLinesAsync(BannedUtxosFilePath, allLines);
			}
			else if (lines.Count != 0) // If we do not have to update the whole thing, we must check if we added a line and so only append.
			{
				await File.AppendAllLinesAsync(BannedUtxosFilePath, lines);
			}
		}

		public async Task UnbanAsync(OutPoint output)
		{
			if (BannedUtxos.TryRemove(output, out _))
			{
				IEnumerable<string> lines = BannedUtxos.Select(x => x.ToString());
				await File.WriteAllLinesAsync(BannedUtxosFilePath, lines);
				Logger.LogInfo($"UTXO unbanned: {output.N}:{output.Hash}.");
			}
		}

		public async Task<BannedUtxoRecord> TryGetBannedAsync(OutPoint outpoint, bool notedToo)
		{
			if (BannedUtxos.TryGetValue(outpoint, out BannedUtxoRecord bannedElem))
			{
				int maxBan = (int)TimeSpan.FromHours(RoundConfig.DosDurationHours).TotalMinutes;
				int banLeftMinutes = maxBan - (int)bannedElem.BannedRemaining.TotalMinutes;
				if (banLeftMinutes > 0)
				{
					if (bannedElem.IsNoted)
					{
						if (notedToo)
						{
							return new BannedUtxoRecord(bannedElem.Utxo, bannedElem.Severity, bannedElem.TimeOfBan, true, bannedElem.BannedForRound);
						}
						else
						{
							return null;
						}
					}
					else
					{
						return new BannedUtxoRecord(bannedElem.Utxo, bannedElem.Severity, bannedElem.TimeOfBan, false, bannedElem.BannedForRound);
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
