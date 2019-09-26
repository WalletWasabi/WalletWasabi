using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Io;
using WalletWasabi.Logging;
using WalletWasabi.Mempool;
using WalletWasabi.Models;

namespace WalletWasabi.Stores
{
	public class TransactionStore
	{
		public string WorkFolderPath { get; private set; }
		public Network Network { get; private set; }

		private Dictionary<uint256, SmartTransaction> MempoolTransactions { get; set; }
		private Dictionary<uint256, SmartTransaction> ConfirmedTransactions { get; set; }

		private object TransactionsLock { get; set; }
		private MutexIoManager ConfirmedTransactionsFileManager { get; set; }
		private MutexIoManager MempoolTransactionsFileManager { get; set; }

		public MempoolService MempoolService { get; private set; }

		public async Task InitializeAsync(string workFolderPath, Network network)
		{
			using (BenchmarkLogger.Measure())
			{
				WorkFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
				Network = Guard.NotNull(nameof(network), network);

				MempoolTransactions = new Dictionary<uint256, SmartTransaction>();
				ConfirmedTransactions = new Dictionary<uint256, SmartTransaction>();
				TransactionsLock = new object();

				MempoolService = new MempoolService();

				//await InitializeAsync(workFolderPath, network, ensureBackwardsCompatibility, "ConfirmedTransactions.dat", () => TryEnsureBackwardsCompatibility(), clearOnRegtest: true);
			}
		}

		//public async Task InitializeAsync(string workFolderPath, Network network, bool ensureBackwardsCompatibility)
		//{
		//	Hashes = new HashSet<uint256>();

		//	await TransactionStore.InitializeAsync(workFolderPath, network, ensureBackwardsCompatibility, "Mempool.dat", () => TryEnsureBackwardsCompatibility(), clearOnRegtest: true);

		//	lock (MempoolLock)
		//	{
		//		foreach (var tx in TransactionStore.GetTransactions())
		//		{
		//			Hashes.Add(tx.GetHash());
		//		}
		//	}

		//}

		//public async Task InitializeAsync(string workFolderPath, Network network, bool ensureBackwardsCompatibility, string fileName, Action tryEnsureBackwardsCompatibilityAction, bool clearOnRegtest)
		//{
		//	fileName = Guard.NotNullOrEmptyOrWhitespace(nameof(fileName), fileName, true);

		//	var transactionsFilePath = Path.Combine(WorkFolderPath, fileName);
		//	TransactionsFileManager = new MutexIoManager(transactionsFilePath);

		//	using (await TransactionsFileManager.Mutex.LockAsync())
		//	{
		//		IoHelpers.EnsureDirectoryExists(WorkFolderPath);

		//		if (ensureBackwardsCompatibility)
		//		{
		//			tryEnsureBackwardsCompatibilityAction();
		//		}

		//		if (clearOnRegtest && Network == Network.RegTest)
		//		{
		//			TransactionsFileManager.DeleteMe(); // RegTest is not a global ledger, better to delete it.
		//		}

		//		if (!TransactionsFileManager.Exists())
		//		{
		//			await SerializeAllTransactionsAsync();
		//		}

		//		await InitializeTransactionsAsync();
		//	}
		//}
	}
}
