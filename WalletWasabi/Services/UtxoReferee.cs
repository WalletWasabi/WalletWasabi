using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.ChaumianCoinJoin;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Services
{
    public class UtxoReferee
    {
		/// <summary>
		/// Key: banned utxo, Value: severity level, time of ban
		/// </summary>
		public ConcurrentDictionary<OutPoint, (int severity, DateTimeOffset timeOfBan)> BannedUtxos { get; }

		public string BannedUtxosFilePath => Path.Combine(FolderPath, $"BannedUtxos{Network}.txt");

		public RPCClient RpcClient { get; }

		public Network Network { get; }

		public string FolderPath { get; }

		public UtxoReferee(Network network, string folderPath, RPCClient rpc)
		{
			Network = Guard.NotNull(nameof(network), network);
			FolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(folderPath), folderPath, trim: true);
			RpcClient = Guard.NotNull(nameof(rpc), rpc);

			BannedUtxos = new ConcurrentDictionary<OutPoint, (int severity, DateTimeOffset timeOfBan)>();

			Directory.CreateDirectory(FolderPath);

			if (File.Exists(BannedUtxosFilePath))
			{
				try
				{
					var toRemove = new List<string>(); // what's been confirmed
					string[] allLines = File.ReadAllLines(BannedUtxosFilePath);
					foreach (string line in allLines)
					{
						var parts = line.Split(':');
						var utxo = new OutPoint(new uint256(parts[3]), int.Parse(parts[2]));
						var severity = int.Parse(parts[1]);
						var timeOfBan = DateTimeOffset.Parse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

						GetTxOutResponse getTxOutResponse = RpcClient.GetTxOut(utxo.Hash, (int)utxo.N, includeMempool: true);

						// Check if inputs are unspent.
						if (getTxOutResponse == null)
						{
							toRemove.Add(line);
						}
						else
						{
							BannedUtxos.TryAdd(utxo, (severity, timeOfBan));
						}
					}

					if (toRemove.Count != 0) // a little performance boost, often it'll be empty
					{
						var newAllLines = allLines.Where(x => !toRemove.Contains(x));
						File.WriteAllLines(BannedUtxosFilePath, newAllLines);
					}

					Logger.LogInfo<UtxoReferee>($"{allLines.Count()} banned UTXOs are loaded from {BannedUtxosFilePath}.");
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

		public async Task BanUtxosAsync(int severity, DateTimeOffset timeOfBan, params OutPoint[] toBan)
		{
			var lines = new List<string>();
			foreach(var utxo in toBan)
			{
				if (BannedUtxos.TryAdd(utxo, (severity, timeOfBan)))
				{
					string line = $"{timeOfBan.ToString(CultureInfo.InvariantCulture)}:{severity}:{utxo.N}:{utxo.Hash}";
					lines.Add(line);
					Logger.LogInfo<UtxoReferee>($"UTXO banned with severity: {severity}. UTXO: {utxo.N}:{utxo.Hash}.");
				}
			}

			if (lines.Count != 0)
			{
				await File.AppendAllLinesAsync(BannedUtxosFilePath, lines);
			}
		}

		public async Task UnbanAsync(OutPoint output)
		{
			if(BannedUtxos.TryRemove(output, out _))
			{
				IEnumerable<string> lines = BannedUtxos.Select(x => $"{x.Value.timeOfBan.ToString(CultureInfo.InvariantCulture)}:{x.Value.severity}:{x.Key.N}:{x.Key.Hash}");
				await File.AppendAllLinesAsync(BannedUtxosFilePath, lines);
				Logger.LogInfo<UtxoReferee>($"UTXO unbanned: {output.N}:{output.Hash}.");
			}
		}
	}
}
