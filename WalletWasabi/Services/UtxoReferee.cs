using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
		/// Key: banned utxo, Value: severity level
		/// </summary>
		public ConcurrentDictionary<OutPoint, int> BannedUtxos { get; }

		public string BannedUtxosFilePath => Path.Combine(FolderPath, $"BannedUtxos{Network}.txt");

		public RPCClient RpcClient { get; }

		public Network Network { get; }

		public string FolderPath { get; }

		public UtxoReferee(Network network, string folderPath, RPCClient rpc)
		{
			Network = Guard.NotNull(nameof(network), network);
			FolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(folderPath), folderPath, trim: true);
			RpcClient = Guard.NotNull(nameof(rpc), rpc);

			BannedUtxos = new ConcurrentDictionary<OutPoint, int>();

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
						var utxo = new OutPoint(new uint256(parts[2]), int.Parse(parts[1]));
						var severity = int.Parse(parts[0]);

						GetTxOutResponse getTxOutResponse = RpcClient.GetTxOut(utxo.Hash, (int)utxo.N, includeMempool: true);

						// Check if inputs are unspent.
						if (getTxOutResponse == null)
						{
							toRemove.Add(line);
						}
						else
						{
							BannedUtxos.TryAdd(utxo, severity);
						}
					}

					if (toRemove.Count != 0) // a little performance boost, often it'll be empty
					{
						var newAllLines = allLines.Where(x => !toRemove.Contains(x));
						File.WriteAllLines(BannedUtxosFilePath, newAllLines);
					}
				}
				catch (Exception ex)
				{
					Logger.LogWarning<UtxoReferee>($"Banned utxo file got corrupted. Deleting {BannedUtxosFilePath}. {ex.GetType()}: {ex.Message}");
					File.Delete(BannedUtxosFilePath);
				}
			}
		}

		public async Task BanAliceAsync(Alice alice)
		{
			var lines = new List<string>();
			foreach(var utxo in alice.Inputs.Select(x=>x.OutPoint))
			{
				BannedUtxos.TryAdd(utxo, 1);
				string line = $"1:{utxo.N}:{utxo.Hash}";
				lines.Add(line);
			}

			await File.AppendAllLinesAsync(BannedUtxosFilePath, lines);
		}
	}
}
