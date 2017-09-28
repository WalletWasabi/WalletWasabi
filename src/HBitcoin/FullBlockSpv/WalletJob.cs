using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentCollections;
using HBitcoin.Helpers;
using HBitcoin.KeyManagement;
using HBitcoin.MemPool;
using HBitcoin.Models;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Newtonsoft.Json.Linq;
using QBitNinja.Client;
using QBitNinja.Client.Models;
using DotNetTor.SocksPort;
using DotNetTor;
using HBitcoin.Fees;

namespace HBitcoin.FullBlockSpv
{
	public class WalletJob
	{
		#region MembersAndProperties

		public static Network CurrentNetwork { get; private set; }

		public Safe Safe { get; private set; }
		public bool TracksDefaultSafe { get; private set; }
		public ConcurrentHashSet<SafeAccount> SafeAccounts { get; private set; }

		public QBitNinjaClient TorQBitClient { get; }
		public QBitNinjaClient NoTorQBitClient { get; }
		public HttpClient TorHttpClient { get; }
		public DotNetTor.ControlPort.Client ControlPortClient { get; }

		public BlockDownloader BlockDownloader;

		public FeeJob FeeJob;

		private Height _creationHeight;
		public Height CreationHeight
		{
			get
			{
				// it's enough to estimate once
				if (_creationHeight != Height.Unknown) return _creationHeight;
				else return _creationHeight = FindSafeCreationHeight();
			}
		}
		private Height FindSafeCreationHeight()
		{
			try
			{
				var currTip = HeaderChain.Tip;
				var currTime = currTip.Header.BlockTime;

				// the chain didn't catch up yet
				if (currTime < Safe.EarliestPossibleCreationTime)
					return Height.Unknown;

				// the chain didn't catch up yet
				if (currTime < Safe.CreationTime)
					return Height.Unknown;

				while (currTime > Safe.CreationTime)
				{
					currTip = currTip.Previous;
					currTime = currTip.Header.BlockTime;
				}

				// when the current tip time is lower than the creation time of the safe let's estimate that to be the creation height
				return new Height(currTip.Height);
			}
			catch
			{
				return Height.Unknown;
			}
		}

		public Height BestHeight => HeaderChain.Height < Tracker.BestHeight ? Height.Unknown : Tracker.BestHeight;
		public event EventHandler BestHeightChanged;
		private void OnBestHeightChanged() => BestHeightChanged?.Invoke(this, EventArgs.Empty);

		public static int ConnectedNodeCount
		{
			get
			{
				if (Nodes == null) return 0;
				return Nodes.ConnectedNodes.Count;
			}
		}
		public static int MaxConnectedNodeCount
		{
			get
			{
				if (Nodes == null) return 0;
				return Nodes.MaximumNodeConnection;
			}
		}
		public static event EventHandler ConnectedNodeCountChanged;
		private static void OnConnectedNodeCountChanged() => ConnectedNodeCountChanged?.Invoke(null, EventArgs.Empty);

		private WalletState _state;
		public WalletState State
		{
			get { return _state; }
			private set
			{
				if (_state == value) return;
				_state = value;
				OnStateChanged();
			}
		}
		public event EventHandler StateChanged;
		private void OnStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

		private static readonly SemaphoreSlim SemaphoreSave = new SemaphoreSlim(1, 1);
		private static NodeConnectionParameters _connectionParameters;
		public static NodesGroup Nodes { get; private set; }

		private const string WorkFolderPath = "FullBlockSpvData";
		private static string _addressManagerFilePath => Path.Combine(WorkFolderPath, $"AddressManager{CurrentNetwork}.dat");
		private static string _headerChainFilePath => Path.Combine(WorkFolderPath, $"HeaderChain{CurrentNetwork}.dat");
		private string _trackerFolderPath => Path.Combine(WorkFolderPath, Safe.UniqueId);

		private Tracker _tracker;
		public Tracker Tracker => GetTrackerAsync().Result;
		// This async getter is for clean exception handling
		private async Task<Tracker> GetTrackerAsync()
		{
			// if already in memory return it
			if (_tracker != null) return _tracker;

			// else load it
			_tracker = new Tracker(Safe.Network);
			try
			{
				await _tracker.LoadAsync(_trackerFolderPath).ConfigureAwait(false);
				EnsureNoMissingRelevantBlocks();
			}
			catch
			{
				// Sync blockchain:
				_tracker = new Tracker(Safe.Network);
			}

			return _tracker;
		}

		private static AddressManager AddressManager
		{
			get
			{
				if (_connectionParameters != null)
				{
					foreach (var behavior in _connectionParameters.TemplateBehaviors)
					{
						if (behavior is AddressManagerBehavior addressManagerBehavior)
							return addressManagerBehavior.AddressManager;
					}
				}
				SemaphoreSave.Wait();
				try
				{
					return AddressManager.LoadPeerFile(_addressManagerFilePath);
				}
				catch
				{
					return new AddressManager();
				}
				finally
				{
					SemaphoreSave.Release();
				}
			}
		}

		public static int GetBlockConfirmations(uint256 blockId)
		{
			var height = 0;
			foreach(var header in HeaderChain.ToEnumerable(fromTip: true))
			{
				if(header.HashBlock == blockId)
				{
					height = header.Height;
				}
			}
			return height;
		}

		private static ConcurrentChain HeaderChain
		{
			get
			{
				if (_connectionParameters != null)
					foreach (var behavior in _connectionParameters.TemplateBehaviors)
					{
						if (behavior is ChainBehavior chainBehavior)
							return chainBehavior.Chain;
					}
				var chain = new ConcurrentChain(CurrentNetwork);
				SemaphoreSave.Wait();
				try
				{
					chain.Load(File.ReadAllBytes(_headerChainFilePath));
				}
				catch
				{
					// ignored
				}
				finally
				{
					SemaphoreSave.Release();
				}

				return chain;
			}
		}

		#endregion

		public WalletJob(SocksPortHandler handler, DotNetTor.ControlPort.Client controlPortClient, Safe safeToTrack, bool trackDefaultSafe = true, params SafeAccount[] accountsToTrack)
		{
			_creationHeight = Height.Unknown;
			_tracker = null;

			Safe = safeToTrack;
			CurrentNetwork = safeToTrack.Network;
			MemPoolJob.Enabled = false;

			NoTorQBitClient = new QBitNinjaClient(safeToTrack.Network);
			TorQBitClient = new QBitNinjaClient(safeToTrack.Network);
			TorQBitClient.SetHttpMessageHandler(handler);
			TorHttpClient = new HttpClient(handler);
			ControlPortClient = controlPortClient;

			FeeJob = new FeeJob(ControlPortClient, TorHttpClient);

			if (accountsToTrack == null || accountsToTrack.Count() < 2)
			{

			}
			if (accountsToTrack == null || !accountsToTrack.Any())
			{
				SafeAccounts = new ConcurrentHashSet<SafeAccount>();
			}
			else SafeAccounts = new ConcurrentHashSet<SafeAccount>(accountsToTrack);

			TracksDefaultSafe = trackDefaultSafe;

			State = WalletState.NotStarted;

			Directory.CreateDirectory(WorkFolderPath);

			Tracker.TrackedTransactions.CollectionChanged += delegate
			{
				UpdateSafeTracking();
			};

			_connectionParameters = new NodeConnectionParameters();
			//So we find nodes faster
			_connectionParameters.TemplateBehaviors.Add(new AddressManagerBehavior(AddressManager));
			//So we don't have to load the chain each time we start
			_connectionParameters.TemplateBehaviors.Add(new ChainBehavior(HeaderChain));
			_connectionParameters.TemplateBehaviors.Add(new MemPoolBehavior());

			UpdateSafeTracking();

			Nodes = new NodesGroup(Safe.Network, _connectionParameters,
				new NodeRequirement
				{
					RequiredServices = NodeServices.Network,
					MinVersion = ProtocolVersion.SENDHEADERS_VERSION
				});
			Nodes.NodeConnectionParameters = _connectionParameters;

			MemPoolJob.Synced += delegate
			{
				State = WalletState.Synced;

				var trackedMemPoolTransactions = Tracker.TrackedTransactions.Where(x => x.Height == Height.MemPool);
				foreach (var tx in trackedMemPoolTransactions)
				{
					// If we are tracking a tx that is malleated or fall out of mempool (too long to confirm) then stop tracking
					if (!MemPoolJob.Transactions.Contains(tx.GetHash()))
					{
						Tracker.TrackedTransactions.TryRemove(tx);
						Debug.WriteLine($"Transaction fall out of MemPool: {tx.GetHash()}");
					}
				}

				Debug.WriteLine("MemPool updated");
			};

			MemPoolJob.NewTransaction += (s, e) =>
			{
				if (Tracker.ProcessTransaction(new SmartTransaction(e.Transaction, Height.MemPool)))
				{
					UpdateSafeTracking();
				}
			};

			Nodes.ConnectedNodes.Removed += delegate { OnConnectedNodeCountChanged(); };
			Nodes.ConnectedNodes.Added += delegate { OnConnectedNodeCountChanged(); };

			Tracker.BestHeightChanged += delegate { OnBestHeightChanged(); };
		}

		public async Task StartAsync(CancellationToken ctsToken)
		{
			State = WalletState.SyncingHeaders;
			Nodes.Connect();

			BlockDownloader = new BlockDownloader();

			var tasks = new HashSet<Task>
			{
				PeriodicSaveAsync(TimeSpan.FromMinutes(3), ctsToken),
				BlockDownloadingJobAsync(ctsToken),
				MemPoolJob.StartAsync(ctsToken),
				BlockDownloader.StartAsync(ctsToken),
				FeeJob.StartAsync(ctsToken)
			};

			await Task.WhenAll(tasks).ConfigureAwait(false);

			State = WalletState.NotStarted;
			await SaveAllChangedAsync().ConfigureAwait(false);
			Nodes.Dispose();
		}

		#region SafeTracking

		// BIP44 specifies default 20, altough we don't use BIP44, let's be somewhat consistent
		public int MaxCleanAddressCount { get; set; } = 20;
		private void UpdateSafeTracking()
		{
			UpdateSafeTrackingByHdPathType(HdPathType.Receive);
			UpdateSafeTrackingByHdPathType(HdPathType.Change);
			UpdateSafeTrackingByHdPathType(HdPathType.NonHardened);
		}

		private void UpdateSafeTrackingByHdPathType(HdPathType hdPathType)
		{
			if (TracksDefaultSafe) UpdateSafeTrackingByPath(hdPathType);

			foreach (var acc in SafeAccounts)
			{
				UpdateSafeTrackingByPath(hdPathType, acc);
			}
		}

		private void UpdateSafeTrackingByPath(HdPathType hdPathType, SafeAccount account = null)
		{
			int i = 0;
			var cleanCount = 0;
			while (true)
			{
				Script scriptPubkey = account == null ? Safe.GetAddress(i, hdPathType).ScriptPubKey : Safe.GetAddress(i, hdPathType, account).ScriptPubKey;

				Tracker.TrackedScriptPubKeys.Add(scriptPubkey);

				// if clean elevate cleancount and if max reached don't look for more
				if (Tracker.IsClean(scriptPubkey))
				{
					cleanCount++;
					if (cleanCount > MaxCleanAddressCount) return;
				}

				i++;
			}
		}

		#endregion

		#region Misc

		/// <summary>
		/// 
		/// </summary>
		/// <param name="account">if null then default safe, if doesn't contain, then exception</param>
		/// <returns></returns>
		public IEnumerable<SafeHistoryRecord> GetSafeHistory(SafeAccount account = null)
		{
			AssertAccount(account);

			var safeHistory = new HashSet<SafeHistoryRecord>();

			var transactions = GetAllChainAndMemPoolTransactionsBySafeAccount(account);
			var scriptPubKeys = GetTrackedScriptPubKeysBySafeAccount(account);

			foreach (SmartTransaction transaction in transactions)
			{
				SafeHistoryRecord record = new SafeHistoryRecord();
				record.TransactionId = transaction.GetHash();
				record.BlockHeight = transaction.Height;

				record.TimeStamp = !transaction.Confirmed
					? transaction.GetFirstSeenIfMemPoolHeight() ?? DateTimeOffset.UtcNow
					: HeaderChain.GetBlock(transaction.Height).Header.BlockTime;

				record.Amount = Money.Zero; //for now

				// how much came to our scriptpubkeys
				foreach (var output in transaction.Transaction.Outputs)
				{
					if (scriptPubKeys.Contains(output.ScriptPubKey))
						record.Amount += output.Value;
				}

				foreach (var input in transaction.Transaction.Inputs)
				{
					// do we have the input?
					SmartTransaction inputTransaction = transactions.FirstOrDefault(x => x.GetHash() == input.PrevOut.Hash);
					if (default(SmartTransaction) != inputTransaction)
					{
						// if yes then deduct from amount (bitcoin output cannot be partially spent)
						var prevOutput = inputTransaction.Transaction.Outputs[input.PrevOut.N];
						if (scriptPubKeys.Contains(prevOutput.ScriptPubKey))
						{
							record.Amount -= prevOutput.Value;
						}
					}
					// if no then whatever
				}

				safeHistory.Add(record);
			}

			return safeHistory.OrderBy(x => x.TimeStamp);
		}

		private void AssertAccount(SafeAccount account)
		{
			if (account == null)
			{
				if (!TracksDefaultSafe)
					throw new NotSupportedException($"{nameof(TracksDefaultSafe)} cannot be {TracksDefaultSafe}");
			}
			else
			{
				if (!SafeAccounts.Any(x => x.Id == account.Id))
					throw new NotSupportedException($"{nameof(SafeAccounts)} does not contain the provided {nameof(account)}");
			}
		}

		public HashSet<SmartTransaction> GetAllChainAndMemPoolTransactionsBySafeAccount(SafeAccount account = null)
		{
			HashSet<Script> trackedScriptPubkeys = GetTrackedScriptPubKeysBySafeAccount(account);
			var foundTransactions = new HashSet<SmartTransaction>();

			foreach (var spk in trackedScriptPubkeys)
			{

				if (TryFindAllChainAndMemPoolTransactions(spk, out HashSet<SmartTransaction> rec, out HashSet<SmartTransaction> spent))
				{
					foreach (var tx in rec)
					{
						foundTransactions.Add(tx);
					}
					foreach (var tx in spent)
					{
						foundTransactions.Add(tx);
					}
				}
			}

			return foundTransactions;
		}

		public HashSet<Script> GetTrackedScriptPubKeysBySafeAccount(SafeAccount account = null)
		{
			var maxTracked = Tracker.TrackedScriptPubKeys.Count;
			var allPossiblyTrackedAddresses = new HashSet<BitcoinAddress>();
			foreach (var address in Safe.GetFirstNAddresses(maxTracked, HdPathType.Receive, account))
			{
				allPossiblyTrackedAddresses.Add(address);
			}
			foreach (var address in Safe.GetFirstNAddresses(maxTracked, HdPathType.Change, account))
			{
				allPossiblyTrackedAddresses.Add(address);
			}
			foreach (var address in Safe.GetFirstNAddresses(maxTracked, HdPathType.NonHardened, account))
			{
				allPossiblyTrackedAddresses.Add(address);
			}

			var actuallyTrackedScriptPubKeys = new HashSet<Script>();
			foreach (var address in allPossiblyTrackedAddresses)
			{
				if (Tracker.TrackedScriptPubKeys.Any(x => x == address.ScriptPubKey))
					actuallyTrackedScriptPubKeys.Add(address.ScriptPubKey);
			}

			return actuallyTrackedScriptPubKeys;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="scriptPubKey"></param>
		/// <param name="receivedTransactions">int: block height</param>
		/// <param name="spentTransactions">int: block height</param>
		/// <returns></returns>
		public bool TryFindAllChainAndMemPoolTransactions(Script scriptPubKey, out HashSet<SmartTransaction> receivedTransactions, out HashSet<SmartTransaction> spentTransactions)
		{
			var found = false;
			receivedTransactions = new HashSet<SmartTransaction>();
			spentTransactions = new HashSet<SmartTransaction>();

			foreach (var tx in Tracker.TrackedTransactions)
			{
				// if already has that tx continue
				if (receivedTransactions.Any(x => x.GetHash() == tx.GetHash()))
					continue;

				foreach (var output in tx.Transaction.Outputs)
				{
					if (output.ScriptPubKey.Equals(scriptPubKey))
					{
						receivedTransactions.Add(tx);
						found = true;
					}
				}
			}

			if (found)
			{
				foreach (var tx in Tracker.TrackedTransactions)
				{
					// if already has that tx continue
					if (spentTransactions.Any(x => x.GetHash() == tx.GetHash()))
						continue;

					foreach (var input in tx.Transaction.Inputs)
					{
						if (receivedTransactions.Select(x => x.GetHash()).Contains(input.PrevOut.Hash))
						{
							spentTransactions.Add(tx);
							found = true;
						}
					}
				}
			}

			return found;
		}

		public static bool TryGetHeader(Height height, out ChainedBlock header)
		{
			header = null;
			try
			{
				if (_connectionParameters == null)
					return false;

				header = HeaderChain.GetBlock(height);

				if (header == null)
					return false;
				else return true;
			}
			catch
			{
				return false;
			}
		}

		public static bool TryGetHeaderHeight(out Height height)
		{
			height = Height.Unknown;
			try
			{
				if (_connectionParameters == null)
					return false;

				height = new Height(HeaderChain.Height);
				return true;
			}
			catch
			{
				return false;
			}
		}

		#endregion

		#region BlockDownloading
		private async Task BlockDownloadingJobAsync(CancellationToken ctsToken)
		{
			MemPoolJob.Enabled = false;
			while (true)
			{
				try
				{
					if (ctsToken.IsCancellationRequested) return;

					// the headerchain didn't catch up to the creationheight yet
					if (Nodes.ConnectedNodes.Count < 3 || // at this condition it might catched up already, neverthless don't progress further
						CreationHeight == Height.Unknown || HeaderChain.Height < CreationHeight)
					{
						State = WalletState.SyncingHeaders;
						await Task.Delay(100, ctsToken).ContinueWith(tsk => { }).ConfigureAwait(false);
						continue;
					}

					Height height;
					Height trackerBestHeight = Tracker.BestHeight;
					var downloadMissing = false;
					if (Tracker.BlockCount == 0)
					{
						height = CreationHeight;
					}
					else if (trackerBestHeight.Type != HeightType.Chain)
					{
						await Task.Delay(100, ctsToken).ContinueWith(tsk => { }).ConfigureAwait(false);
						continue;
					}
					else if (_missingBlocks.Count() != 0)
					{
						downloadMissing = true;
						height = new Height(_missingBlocks.Min());
					}
					else
					{
						Height unprocessedBlockBestHeight = Tracker.UnprocessedBlockBuffer.BestHeight;
						// if no blocks to download (or process) start syncing mempool
						if (HeaderChain.Height <= trackerBestHeight)
						{
							State = MemPoolJob.SyncedOnce ? WalletState.Synced : WalletState.SyncingMemPool;
							MemPoolJob.Enabled = true;
							await Task.Delay(100, ctsToken).ContinueWith(tsk => { }).ConfigureAwait(false);
							continue;
						}
						// else sync blocks
						else
						{
							MemPoolJob.Enabled = false;
							State = WalletState.SyncingBlocks;
							// if unprocessed buffer hit the headerchain height
							// or unprocessed buffer is full
							// wait until they get processed
							if ((
									unprocessedBlockBestHeight.Type == HeightType.Chain
									&& (HeaderChain.Height <= unprocessedBlockBestHeight)
								)
								|| Tracker.UnprocessedBlockBuffer.Full)
							{
								await Task.Delay(100, ctsToken).ContinueWith(tsk => { }).ConfigureAwait(false);
								continue;
							}
							// else figure out the next block's height to download
							else
							{
								int relevant = unprocessedBlockBestHeight.Type == HeightType.Chain
									? unprocessedBlockBestHeight.Value
									: 0;

								// should not happen at this point, but better to check
								if (trackerBestHeight.Type != HeightType.Chain)
								{
									await Task.Delay(100, ctsToken).ContinueWith(tsk => { }).ConfigureAwait(false);
									continue;
								}

								height = new Height(
									Math.Max(trackerBestHeight.Value, relevant)
									+ 1);
							}
						}
					}

					var firstHeight = height;
					Height lookAheadHeight;
					if (downloadMissing)
					{
						lookAheadHeight = height;
					}
					else
					{
						lookAheadHeight = new Height(HeaderChain.Height);
					}

					var block = await BlockDownloader.TakeBlockAsync(firstHeight, lookAheadHeight, ctsToken).ConfigureAwait(false);

					if (ctsToken.IsCancellationRequested) return;

					if (block == null)
					{
						// should not happen
						await Task.Delay(1000).ConfigureAwait(false);
						continue;
					}

					if (ctsToken.IsCancellationRequested) return;

					// if the hash of the downloaded block is not the same as the header's
					// if the proof of work and merkle root isn't valid
					if (HeaderChain.GetBlock(height).HashBlock != block.GetHash()
						|| !block.Check())
					{
						Reorg();
						continue;
					}

					Tracker.AddOrReplaceBlock(height, block);
					MemPoolJob.RemoveTransactions(block.Transactions.Select(x => x.GetHash()));

					if (downloadMissing)
					{
						_missingBlocks.TryRemove(height.Value);
						Debug.WriteLine($"Downloaded missing block: {height.Value}");
					}
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"Ignoring {nameof(BlockDownloadingJobAsync)} exception:");
					Debug.WriteLine(ex);
				}
			}
		}

		// keep it int for better performance
		private ConcurrentHashSet<int> _missingBlocks = new ConcurrentHashSet<int>();
		private void EnsureNoMissingRelevantBlocks()
		{
			// must be this complicated for performance
			var neededHeights = new HashSet<int>();
			foreach (var h in Tracker.TrackedTransactions
				.Where(x => x.Height.Type == HeightType.Chain)
				.Select(x => x.Height))
			{
				neededHeights.Add(h.Value);
			}

			foreach (var h in Tracker.MerkleChain.Select(x => x.Height.Value))
			{
				if (neededHeights.Contains(h))
				{
					neededHeights.Remove(h);
				}
			}
			foreach (var h in neededHeights)
			{
				_missingBlocks.Add(h);
			}
		}

		private void Reorg()
		{
			BlockDownloader.Clear();
			HeaderChain.SetTip(HeaderChain.Tip.Previous);
			Tracker.ReorgOne();
			SaveAllChangedAsync().Wait();
		}
		#endregion

		#region Saving
		private async Task PeriodicSaveAsync(TimeSpan delay, CancellationToken ctsToken)
		{
			while (true)
			{
				try
				{
					if (ctsToken.IsCancellationRequested) return;

					await SaveAllChangedAsync().ConfigureAwait(false);

					await Task.Delay(delay, ctsToken).ContinueWith(tsk => { }).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"Ignoring {nameof(PeriodicSaveAsync)} exception:");
					Debug.WriteLine(ex);
				}
			}
		}

		private Height _savedHeaderHeight = Height.Unknown;
		private int _savedTrackerBlockCount = -1;

		private async Task SaveAllChangedAsync()
		{
			await SemaphoreSave.WaitAsync().ConfigureAwait(false);
			try
			{
				AddressManager.SavePeerFile(_addressManagerFilePath, Safe.Network);
				Debug.WriteLine($"Saved {nameof(AddressManager)}");

				if (_connectionParameters != null)
				{
					var headerHeight = new Height(HeaderChain.Height);
					if (_savedHeaderHeight == Height.Unknown || headerHeight > _savedHeaderHeight)
					{
						SaveHeaderChain();
						Debug.WriteLine($"Saved {nameof(HeaderChain)} at height: {headerHeight}");
						_savedHeaderHeight = headerHeight;
					}
				}
			}
			finally
			{
				SemaphoreSave.Release();
			}

			var bestHeight = BestHeight;
			var trackerBlockCount = Tracker.BlockCount;
			if (bestHeight.Type == HeightType.Chain
				&& (_savedTrackerBlockCount == -1
					|| trackerBlockCount > _savedTrackerBlockCount))
			{
				await Tracker.SaveAsync(_trackerFolderPath).ConfigureAwait(false);
				Debug.WriteLine($"Saved {nameof(Tracker)} at height: {bestHeight} and block count: {trackerBlockCount}");
				_savedTrackerBlockCount = trackerBlockCount;
			}
		}

		private static void SaveHeaderChain()
		{
			using (var fs = File.Open(_headerChainFilePath, FileMode.Create))
			{
				HeaderChain.WriteTo(fs);
			}
		}
		#endregion

		#region TransactionSending

		/// <summary>
		/// 
		/// </summary>
		/// <param name="scriptPubKeyToSend"></param>
		/// <param name="amount">If Money.Zero then spend all available amount</param>
		/// <param name="feeType"></param>
		/// <param name="account"></param>
		/// <param name="allowUnconfirmed">Allow to spend unconfirmed transactions, if necessary</param>
		/// <returns></returns>
		public async Task<BuildTransactionResult> BuildTransactionAsync(Script scriptPubKeyToSend, Money amount, FeeType feeType, SafeAccount account = null, bool allowUnconfirmed = false)
		{
			try
			{
				AssertAccount(account);

				// 1. Get the script pubkey of the change.
				Debug.WriteLine("Select change address...");
				Script changeScriptPubKey = GetUnusedScriptPubKeys(account, HdPathType.Change).FirstOrDefault();

				// 2. Find all coins I can spend from the account
				// 3. How much money we can spend?
				Debug.WriteLine("Calculating available amount...");
				AvailableAmount balance = GetBalance(out IDictionary<Coin, bool> unspentCoins, account);
				Money spendableConfirmedAmount = balance.Confirmed;
				Money spendableUnconfirmedAmount =
					allowUnconfirmed ? balance.Unconfirmed : Money.Zero;
				Debug.WriteLine($"Spendable confirmed amount: {spendableConfirmedAmount}");
				Debug.WriteLine($"Spendable unconfirmed amount: {spendableUnconfirmedAmount}");

				BuildTransactionResult successfulResult = new BuildTransactionResult
				{
					Success = true,
					FailingReason = ""
				};

				// 4. Get and calculate fee
				Debug.WriteLine("Calculating dynamic transaction fee...");
				Money feePerBytes = null;
				try
				{
					feePerBytes = await FeeJob.GetFeePerBytesAsync(feeType, new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					Debug.WriteLine(ex.Message);
					return new BuildTransactionResult
					{
						Success = false,
						FailingReason = $"Couldn't calculate transaction fee. Reason:{Environment.NewLine}{ex}"
					};
				}

				bool spendAll = amount == Money.Zero;
				int inNum;
				if (spendAll)
				{
					inNum = unspentCoins.Count;
				}
				else
				{
					const int expectedMinTxSize = 1 * 148 + 2 * 34 + 10 - 1;
					try
					{
						inNum = SelectCoinsToSpend(unspentCoins, amount + feePerBytes * expectedMinTxSize).Count;
					}
					catch (InsufficientBalanceException)
					{
						return NotEnoughFundsBuildTransactionResult;
					}
				}

				const int outNum = 2; // 1 address to send + 1 for change
				var estimatedTxSize = inNum * 148 + outNum * 34 + 10 + inNum; // http://bitcoin.stackexchange.com/questions/1195/how-to-calculate-transaction-size-before-sending
				Debug.WriteLine($"Estimated tx size: {estimatedTxSize} bytes");
				Money fee = feePerBytes * estimatedTxSize;
				Debug.WriteLine($"Fee: {fee.ToDecimal(MoneyUnit.BTC):0.#############################}btc");
				successfulResult.Fee = fee;

				// 5. How much to spend?
				Money amountToSend = null;
				if (spendAll)
				{
					if (allowUnconfirmed)
						amountToSend = spendableConfirmedAmount + spendableUnconfirmedAmount;
					else
						amountToSend = spendableConfirmedAmount;
					amountToSend -= fee;
				}
				else
				{
					amountToSend = amount;
				}

				// 6. Do some checks
				if (amountToSend < Money.Zero)
				{
					return NotEnoughFundsBuildTransactionResult;
				}
				if (allowUnconfirmed)
				{
					if (spendableConfirmedAmount + spendableUnconfirmedAmount < amountToSend + fee)
					{
						return NotEnoughFundsBuildTransactionResult;
					}
				}
				else
				{
					if (spendableConfirmedAmount < amountToSend + fee)
					{
						return NotEnoughFundsBuildTransactionResult;
					}
				}

				decimal feePc = (100 * fee.ToDecimal(MoneyUnit.BTC)) / amountToSend.ToDecimal(MoneyUnit.BTC);
				successfulResult.FeePercentOfSent = feePc;
				if (feePc > 1)
				{
					Debug.WriteLine("");
					Debug.WriteLine($"The transaction fee is {feePc:0.#}% of your transaction amount.");
					Debug.WriteLine($"Sending:\t {amountToSend.ToDecimal(MoneyUnit.BTC):0.#############################}btc");
					Debug.WriteLine($"Fee:\t\t {fee.ToDecimal(MoneyUnit.BTC):0.#############################}btc");
				}

				var confirmedAvailableAmount = spendableConfirmedAmount - spendableUnconfirmedAmount;
				var totalOutAmount = amountToSend + fee;
				if (confirmedAvailableAmount < totalOutAmount)
				{
					var unconfirmedToSend = totalOutAmount - confirmedAvailableAmount;
					Debug.WriteLine("");
					Debug.WriteLine($"In order to complete this transaction you have to spend {unconfirmedToSend.ToDecimal(MoneyUnit.BTC):0.#############################} unconfirmed btc.");
					successfulResult.SpendsUnconfirmed = true;
				}

				// 7. Select coins
				Debug.WriteLine("Selecting coins...");
				HashSet<Coin> coinsToSpend = SelectCoinsToSpend(unspentCoins, totalOutAmount);

				// 8. Get signing keys
				var signingKeys = new HashSet<ISecret>();
				foreach (var coin in coinsToSpend)
				{
					var signingKey = Safe.FindPrivateKey(coin.ScriptPubKey.GetDestinationAddress(Safe.Network), Tracker.TrackedScriptPubKeys.Count, account);
					signingKeys.Add(signingKey);
				}

				// 9. Build the transaction
				Debug.WriteLine("Signing transaction...");
				var builder = new TransactionBuilder();
				var tx = builder
					.AddCoins(coinsToSpend)
					.AddKeys(signingKeys.ToArray())
					.Send(scriptPubKeyToSend, amountToSend)
					.SetChange(changeScriptPubKey)
					.SendFees(fee)
					.BuildTransaction(true);

				if (!builder.Verify(tx))
					return new BuildTransactionResult
					{
						Success = false,
						FailingReason = "Couldn't build the transaction"
					};

				successfulResult.Transaction = tx;
				return successfulResult;
			}
			catch (Exception ex)
			{
				return new BuildTransactionResult
				{
					Success = false,
					FailingReason = ex.ToString()
				};
			}
		}

		private static BuildTransactionResult NotEnoughFundsBuildTransactionResult =>
			new BuildTransactionResult
			{
				Success = false,
				FailingReason = "Not enough funds"
			};

		public IEnumerable<Script> GetUnusedScriptPubKeys(SafeAccount account = null, HdPathType hdPathType = HdPathType.Receive)
		{
			AssertAccount(account);

			HashSet<Script> scriptPubKeys = new HashSet<Script>();
			int i = 0;
			while (true)
			{
				Script scriptPubkey = account == null ? Safe.GetAddress(i, hdPathType).ScriptPubKey : Safe.GetAddress(i, hdPathType, account).ScriptPubKey;
				if (Tracker.IsClean(scriptPubkey))
				{
					scriptPubKeys.Add(scriptPubkey);
					if (scriptPubKeys.Count >= MaxCleanAddressCount)
						return scriptPubKeys;
				}
				i++;
			}
		}

		internal static HashSet<Coin> SelectCoinsToSpend(IDictionary<Coin, bool> unspentCoins, Money totalOutAmount)
		{
			var coinsToSpend = new HashSet<Coin>();
			var unspentConfirmedCoins = new List<Coin>();
			var unspentUnconfirmedCoins = new List<Coin>();
			foreach (var elem in unspentCoins)
				if (elem.Value) unspentConfirmedCoins.Add(elem.Key);
				else unspentUnconfirmedCoins.Add(elem.Key);

			bool haveEnough = SelectCoins(ref coinsToSpend, totalOutAmount, unspentConfirmedCoins);
			if (!haveEnough)
				haveEnough = SelectCoins(ref coinsToSpend, totalOutAmount, unspentUnconfirmedCoins);
			if (!haveEnough)
				throw new InsufficientBalanceException();

			return coinsToSpend;
		}
		private static bool SelectCoins(ref HashSet<Coin> coinsToSpend, Money totalOutAmount, IEnumerable<Coin> unspentCoins)
		{
			var haveEnough = false;
			foreach (var coin in unspentCoins.OrderByDescending(x => x.Amount))
			{
				coinsToSpend.Add(coin);
				// if doesn't reach amount, continue adding next coin
				if (coinsToSpend.Sum(x => x.Amount) < totalOutAmount) continue;

				haveEnough = true;
				break;
			}

			return haveEnough;
		}

		public AvailableAmount GetBalance(out IDictionary<Coin, bool> unspentCoins, SafeAccount account = null)
		{
			// 1. Find all coins I can spend from the account
			Debug.WriteLine("Finding all unspent coins...");
			unspentCoins = GetUnspentCoins(account);

			// 2. How much money we can spend?
			var confirmedAvailableAmount = Money.Zero;
			var unconfirmedAvailableAmount = Money.Zero;
			foreach (var elem in unspentCoins)
			{
				// Value true if confirmed
				if (elem.Value)
				{
					confirmedAvailableAmount += elem.Key.Amount as Money;
				}
				else
				{
					unconfirmedAvailableAmount += elem.Key.Amount as Money;
				}
			}

			return new AvailableAmount
			{
				Confirmed = confirmedAvailableAmount,
				Unconfirmed = unconfirmedAvailableAmount
			};
		}

		public struct BuildTransactionResult
		{
			public bool Success;
			public string FailingReason;
			public Transaction Transaction;
			public bool SpendsUnconfirmed;
			public Money Fee;
			public decimal FeePercentOfSent;
		}
		public struct AvailableAmount
		{
			public Money Confirmed;
			public Money Unconfirmed;
		}

		/// <summary>
		/// Find all unspent transaction output of the account
		/// </summary>
		public IDictionary<Coin, bool> GetUnspentCoins(SafeAccount account = null)
		{
			AssertAccount(account);

			var unspentCoins = new Dictionary<Coin, bool>();

			var trackedScriptPubkeys = GetTrackedScriptPubKeysBySafeAccount(account);

			// 1. Go through all the transactions and their outputs
			foreach (SmartTransaction tx in Tracker
				.TrackedTransactions
				.Where(x => x.Height != Height.Unknown))
			{
				foreach (var coin in tx.Transaction.Outputs.AsCoins())
				{
					// 2. Check if the coin comes with our account
					if (trackedScriptPubkeys.Contains(coin.ScriptPubKey))
					{
						// 3. Check if coin is unspent, if so add to our utxoSet
						if (IsUnspent(coin))
						{
							unspentCoins.Add(coin, tx.Confirmed);
						}
					}
				}
			}

			return unspentCoins;
		}

		private bool IsUnspent(Coin coin) => Tracker
			.TrackedTransactions
			.Where(x => x.Height.Type == HeightType.Chain || x.Height.Type == HeightType.MemPool)
			.SelectMany(x => x.Transaction.Inputs)
			.All(txin => txin.PrevOut != coin.Outpoint);

		public async Task<SendTransactionResult> SendTransactionAsync(Transaction tx)
		{
			if (State < WalletState.SyncingMemPool)
			{
				return new SendTransactionResult
				{
					Success = false,
					FailingReason =
						"Only propagate transactions after all blocks are downloaded"
				};
			}

			try
			{
				await ControlPortClient.ChangeCircuitAsync().ConfigureAwait(false);
				var successfulResult = new SendTransactionResult
				{
					Success = true,
					FailingReason = ""
				};
				Debug.WriteLine($"Transaction Id: {tx.GetHash()}");

				// times out at 21sec, last is smartbit, doesn't check for responses, they are sometimes buggy
				var counter = 0;
				while (true)
				{
					HttpResponseMessage smartBitResponse = new HttpResponseMessage();
					BroadcastResponse qbitResponse = new BroadcastResponse();
					try
					{
						Debug.Write("Broadcasting with ");
						if (counter % 2 == 0)
						{
							Debug.WriteLine("QBit...");
							qbitResponse = await TorQBitClient.Broadcast(tx).ConfigureAwait(false);
						}
						else
						{
							Debug.WriteLine("SmartBit...");
							var post = "https://testnet-api.smartbit.com.au/v1/blockchain/pushtx";
							if (CurrentNetwork == Network.Main)
								post = "https://api.smartbit.com.au/v1/blockchain/pushtx";

							var content = new StringContent(new JObject(new JProperty("hex", tx.ToHex())).ToString(), Encoding.UTF8,
								"application/json");
							smartBitResponse = await TorHttpClient.PostAsync(post, content).ConfigureAwait(false);
						}
					}
					catch
					{
						counter++;
						continue;
					}
					await Task.Delay(1000).ConfigureAwait(false);
					var arrived = MemPoolJob.Transactions.Contains(tx.GetHash());
					if (arrived)
					{
						Debug.WriteLine("Transaction is successfully propagated on the network.");
						return successfulResult;
					}

					if (counter >= 21) // keep it odd, smartbit has more reliable error messages
					{
						string reason = "";
						if (counter % 2 == 0) //qbit
						{
							reason = $"Success: {qbitResponse.Success}";
							if (qbitResponse.Error != null)
							{
								reason += $", ErrorCode: {qbitResponse.Error.ErrorCode}, Reason: {qbitResponse.Error.Reason}";
							}
						}
						else //smartbit
						{
							var json = JObject.Parse(await smartBitResponse.Content.ReadAsStringAsync().ConfigureAwait(false));
							if (json.Value<bool>("success"))
							{
								Debug.WriteLine("Transaction is successfully propagated on the network.");
								return successfulResult;
							}
							else
							{
								Debug.WriteLine(
									$"Error code: {json["error"].Value<string>("code")} Reason: {json["error"].Value<string>("message")}");
							}
							reason = $"Success: { json.Value<bool>("success") } Error code: {json["error"].Value<string>("code")} Reason: {json["error"].Value<string>("message")}";
						}
						return new SendTransactionResult
						{
							Success = false,
							FailingReason =
							$"The transaction might not have been successfully broadcasted. Check the Transaction ID in a block explorer. Details: {reason}"
						};
					}
					counter++;
				}
			}
			catch (Exception ex)
			{
				return new SendTransactionResult
				{
					Success = false,
					FailingReason =
						"The transaction might not have been successfully broadcasted. Check the Transaction ID in a block explorer. Details:" + ex.ToString()
				};
			}
		}

		public struct SendTransactionResult
		{
			public bool Success;
			public string FailingReason;
		}

		#endregion
	}
}
