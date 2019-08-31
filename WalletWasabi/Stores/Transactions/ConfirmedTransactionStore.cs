using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Stores.Transactions
{
	public class ConfirmedTransactionStore : TransactionStore, IStore
	{
		public async Task InitializeAsync(string workFolderPath, Network network, bool ensureBackwardsCompatibility)
		{
			var initStart = DateTimeOffset.UtcNow;

			await InitializeAsync(workFolderPath, network, ensureBackwardsCompatibility, "ConfirmedTransactions.dat", () => TryEnsureBackwardsCompatibility(), clearOnRegtest: true);

			var elapsedSeconds = Math.Round((DateTimeOffset.UtcNow - initStart).TotalSeconds, 1);
			Logger.LogInfo<ConfirmedTransactionStore>($"Initialized in {elapsedSeconds} seconds.");
		}

		private void TryEnsureBackwardsCompatibility()
		{
			try
			{
				// Before Wasabi 1.1.7
				var oldTransactionsFolderPath = Path.Combine(EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client")), "Transactions", Network.Name);
				if (Directory.Exists(oldTransactionsFolderPath))
				{
					foreach (var filePath in Directory.EnumerateFiles(oldTransactionsFolderPath))
					{
						try
						{
							string jsonString = File.ReadAllText(filePath, Encoding.UTF8);
							var confirmedTransactions = JsonConvert.DeserializeObject<IEnumerable<SmartTransaction>>(jsonString)?.Where(x => x.Confirmed)?.OrderByBlockchain() ?? Enumerable.Empty<SmartTransaction>();
							lock (TransactionsLock)
							{
								TryAddNoLockNoSerialization(confirmedTransactions);
							}
						}
						catch (Exception ex)
						{
							Logger.LogTrace<MempoolStore>(ex);
						}
						// Do not delete, because you don't know if the mempool compatibiltiy has been initialized yet.
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning<MempoolStore>($"Backwards compatibility could not be ensured. Exception: {ex}.");
			}
		}
	}
}
