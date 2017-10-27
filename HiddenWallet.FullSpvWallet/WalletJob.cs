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
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Newtonsoft.Json.Linq;
using QBitNinja.Client;
using QBitNinja.Client.Models;
using DotNetTor.SocksPort;
using DotNetTor;
using HiddenWallet.Models;
using HiddenWallet.KeyManagement;
using HiddenWallet.FullSpv.MemPool;
using HiddenWallet.FullSpv.Fees;
using Nito.AsyncEx;
using HiddenWallet.WebClients.SmartBit;

namespace HiddenWallet.FullSpv
{
	public class WalletJob
	{
		#region MembersAndProperties

		public Network CurrentNetwork { get; private set; }

		public Safe Safe { get; private set; }
		public bool TracksDefaultSafe { get; private set; }
		public ConcurrentHashSet<SafeAccount> SafeAccounts { get; private set; }
		
		public SmartBitClient TorSmartBitClient { get; private set; }
		public DotNetTor.ControlPort.Client ControlPortClient { get; private set; }

		public BlockDownloader BlockDownloader;

		public FeeService FeeService;

		private Height _creationHeight;
		public async Task<Height> GetCreationHeightAsync()
		{
            // it's enough to estimate once
            if (_creationHeight != Height.Unknown) return _creationHeight;
            else return _creationHeight = await FindSafeCreationHeightAsync();
        }
		private async Task<Height> FindSafeCreationHeightAsync()
		{
			try
			{
				var currTip = (await GetHeaderChainAsync()).Tip;
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
            catch (Exception)
            {
                return Height.Unknown;
            }
        }

        public async Task<Height> GetBestHeightAsync()
        {
            var tracker = (await GetTrackerAsync());
            return (await GetHeaderChainAsync()).Height < tracker.BestHeight ? Height.Unknown : tracker.BestHeight;
        }
		public event EventHandler BestHeightChanged;
		private void OnBestHeightChanged() => BestHeightChanged?.Invoke(this, EventArgs.Empty);

		public int ConnectedNodeCount
		{
			get
			{
				if (Nodes == null) return 0;
				return Nodes.ConnectedNodes.Count;
			}
		}
		public int MaxConnectedNodeCount
		{
			get
			{
				if (Nodes == null) return 0;
				return Nodes.MaximumNodeConnection;
			}
		}
		public event EventHandler ConnectedNodeCountChanged;
		private void OnConnectedNodeCountChanged() => ConnectedNodeCountChanged?.Invoke(this, EventArgs.Empty);

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

        private readonly AsyncLock _asyncLockSave = new AsyncLock();
		private NodeConnectionParameters _connectionParameters;
		public NodesGroup Nodes { get; private set; }

		private string WorkFolderPath => Path.Combine(FullSpvWallet.Global.DataDir, "FullBlockSpvData");
		private string _addressManagerFilePath => Path.Combine(WorkFolderPath, $"AddressManager{CurrentNetwork}.dat");
		private string _headerChainFilePath => Path.Combine(WorkFolderPath, $"HeaderChain{CurrentNetwork}.dat");
		private string _trackerFolderPath => Path.Combine(WorkFolderPath, Safe.UniqueId);

		private Tracker _tracker;
		public async Task<Tracker> GetTrackerAsync()
		{
			// if already in memory return it
			if (_tracker != null) return _tracker;

			// else load it
			_tracker = new Tracker(Safe.Network);
			try
			{
				await _tracker.LoadAsync(_trackerFolderPath);
				await EnsureNoMissingRelevantBlocksAsync();
			}
            catch (Exception)
            {
                // Sync blockchain:
                _tracker = new Tracker(Safe.Network);
            }

            return _tracker;
		}

        public MemPoolJob MemPoolJob { get; private set; }

        public AddressManager GetAddressManager()
        {
            if (_connectionParameters != null)
            {
                foreach (var behavior in _connectionParameters.TemplateBehaviors)
                {
                    if (behavior is AddressManagerBehavior addressManagerBehavior)
                        return addressManagerBehavior.AddressManager;
                }
            }

            using (_asyncLockSave.Lock())
            {
                try
                {
                    return AddressManager.LoadPeerFile(_addressManagerFilePath);
                }
                catch (Exception)
                {
                    return new AddressManager();
                }
            }
        }

		public async Task<int> GetBlockConfirmationsAsync(uint256 blockId)
		{
			var height = 0;
			foreach(var header in (await GetHeaderChainAsync()).ToEnumerable(fromTip: true))
			{
				if(header.HashBlock == blockId)
				{
					height = header.Height;
				}
			}
			return height;
		}

		private async Task<ConcurrentChain> GetHeaderChainAsync()
		{
            if (_connectionParameters != null)
            {
                foreach (var behavior in _connectionParameters.TemplateBehaviors)
                {
                    if (behavior is ChainBehavior chainBehavior)
                        return chainBehavior.Chain;
                }
            }

            var chain = new ConcurrentChain(CurrentNetwork);
            using (await _asyncLockSave.LockAsync())
            {
                try
                {
                    chain.Load(await File.ReadAllBytesAsync(_headerChainFilePath));
                }
                catch
                {
                    // ignored
                }
            }

            return chain;
        }

        #endregion

        public WalletJob()
        {

        }

		public async Task InitializeAsync(SocksPortHandler handler, DotNetTor.ControlPort.Client controlPortClient, Safe safeToTrack, bool trackDefaultSafe = true, params SafeAccount[] accountsToTrack)
		{
			_creationHeight = Height.Unknown;
			_tracker = null;

			Safe = safeToTrack;
			CurrentNetwork = safeToTrack.Network;
            MemPoolJob = new MemPoolJob(this)
            {
                Enabled = false
            };
			
			ControlPortClient = controlPortClient;
			TorSmartBitClient = new SmartBitClient(safeToTrack.Network, handler, false);

			FeeService = new FeeService(safeToTrack.Network, ControlPortClient, disposeTorControl: false, handler: handler, disposeHandler: false);

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

            (await GetTrackerAsync()).TrackedTransactions.CollectionChanged += TrackedTransactions_CollectionChangedAsync;

			_connectionParameters = new NodeConnectionParameters();
			//So we find nodes faster
			_connectionParameters.TemplateBehaviors.Add(new AddressManagerBehavior(GetAddressManager()));
			//So we don't have to load the chain each time we start
			_connectionParameters.TemplateBehaviors.Add(new ChainBehavior(await GetHeaderChainAsync()));
			_connectionParameters.TemplateBehaviors.Add(new MemPoolBehavior(MemPoolJob));

			await UpdateSafeTrackingAsync();

            Nodes = new NodesGroup(Safe.Network, _connectionParameters,
                new NodeRequirement
                {
                    RequiredServices = NodeServices.Network,
                    MinVersion = ProtocolVersion.SENDHEADERS_VERSION
                })
            {
                NodeConnectionParameters = _connectionParameters
            };

            MemPoolJob.Synced += MemPoolJob_SyncedAsync;
            MemPoolJob.NewTransaction += MemPoolJob_NewTransactionAsync;

            Nodes.ConnectedNodes.Removed += ConnectedNodes_Removed;
            Nodes.ConnectedNodes.Added += ConnectedNodes_Added;

            (await GetTrackerAsync()).BestHeightChanged += WalletJob_BestHeightChanged;
		}

        private async void TrackedTransactions_CollectionChangedAsync(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            await UpdateSafeTrackingAsync();
        }

        private void WalletJob_BestHeightChanged(object sender, EventArgs e)
        {
            OnBestHeightChanged();
        }

        private void ConnectedNodes_Added(object sender, NodeEventArgs e)
        {
            OnConnectedNodeCountChanged();
        }

        private void ConnectedNodes_Removed(object sender, NodeEventArgs e)
        {
            OnConnectedNodeCountChanged();
        }

        private async void MemPoolJob_NewTransactionAsync(object sender, NewTransactionEventArgs e)
        {
            if ((await GetTrackerAsync()).ProcessTransaction(new SmartTransaction(e.Transaction, Height.MemPool)))
            {
                await UpdateSafeTrackingAsync();
            }
        }

        private async void MemPoolJob_SyncedAsync(object sender, EventArgs e)
        {
            State = WalletState.Synced;

            var trackedMemPoolTransactions = (await GetTrackerAsync()).TrackedTransactions.Where(x => x.Height == Height.MemPool);
            foreach (var tx in trackedMemPoolTransactions)
            {
                // If we are tracking a tx that is malleated or fall out of mempool (too long to confirm) then stop tracking
                if (!MemPoolJob.Transactions.Contains(tx.GetHash()))
                {
                    (await GetTrackerAsync()).TrackedTransactions.TryRemove(tx);
                    Debug.WriteLine($"Transaction fall out of MemPool: {tx.GetHash()}");
                }
            }

            Debug.WriteLine("MemPool updated");
        }

        public async Task StartAsync(CancellationToken ctsToken)
        {
            var tasks = new HashSet<Task>();
            try
            {
                State = WalletState.SyncingHeaders;
                Nodes.Connect();

                BlockDownloader = new BlockDownloader(this);

                tasks = new HashSet<Task>
            {
                PeriodicSaveAsync(TimeSpan.FromMinutes(3), ctsToken),
                BlockDownloadingJobAsync(ctsToken),
                MemPoolJob.StartAsync(ctsToken),
                BlockDownloader.StartAsync(ctsToken),
                FeeService.StartAsync(ctsToken)
            };

                await Task.WhenAll(tasks);
            }
            finally
            {
                State = WalletState.NotStarted;
                (await GetTrackerAsync()).TrackedTransactions.CollectionChanged -= TrackedTransactions_CollectionChangedAsync;
                MemPoolJob.Synced -= MemPoolJob_SyncedAsync;
                MemPoolJob.NewTransaction -= MemPoolJob_NewTransactionAsync;
                Nodes.ConnectedNodes.Removed -= ConnectedNodes_Removed;
                Nodes.ConnectedNodes.Added -= ConnectedNodes_Added;
                (await GetTrackerAsync()).BestHeightChanged -= WalletJob_BestHeightChanged;
                await SaveAllChangedAsync();
                Nodes?.Dispose();
                foreach(var task in tasks)
                {
                    task?.Dispose();
                }
                FeeService?.Dispose();
				TorSmartBitClient?.Dispose();
                try
                {
                    ControlPortClient?.DisconnectDisposeSocket();
                }
                catch (Exception)
                {

                }
            }
        }

		#region SafeTracking

		// BIP44 specifies default 20, altough we don't use BIP44, let's be somewhat consistent
		public int MaxCleanAddressCount { get; set; } = 20;
		private async Task UpdateSafeTrackingAsync()
		{
            await UpdateSafeTrackingByHdPathTypeAsync(HdPathType.Receive);
            await UpdateSafeTrackingByHdPathTypeAsync(HdPathType.Change);
            await UpdateSafeTrackingByHdPathTypeAsync(HdPathType.NonHardened);
		}

		private async Task UpdateSafeTrackingByHdPathTypeAsync(HdPathType hdPathType)
		{
			if (TracksDefaultSafe) await UpdateSafeTrackingByPathAsync(hdPathType);

			foreach (var acc in SafeAccounts)
			{
				await UpdateSafeTrackingByPathAsync(hdPathType, acc);
			}
		}

        private async Task UpdateSafeTrackingByPathAsync(HdPathType hdPathType, SafeAccount account = null)
        {
            var addressTypes = new HashSet<AddressType>
            {
                AddressType.Pay2PublicKeyHash,
                AddressType.Pay2WitnessPublicKeyHash
            };
            foreach (AddressType addressType in addressTypes)
            {
                int i = 0;
                var cleanCount = 0;
                while (true)
                {
                    Script scriptPubkey = account == null ? Safe.GetAddress(addressType, i, hdPathType).ScriptPubKey : Safe.GetAddress(addressType, i, hdPathType, account).ScriptPubKey;

                    (await GetTrackerAsync()).TrackedScriptPubKeys.Add(scriptPubkey);

                    // if clean elevate cleancount and if max reached don't look for more
                    if ((await GetTrackerAsync()).IsClean(scriptPubkey))
                    {
                        cleanCount++;
                        if (cleanCount > MaxCleanAddressCount) break;
                    }

                    i++;
                }
            }
        }

		#endregion

		#region Misc

		/// <summary>
		///
		/// </summary>
		/// <param name="account">if null then default safe, if doesn't contain, then exception</param>
		/// <returns></returns>
		public async Task<IEnumerable<WalletHistoryRecord>> GetSafeHistoryAsync(SafeAccount account = null)
		{
			AssertAccount(account);

			var safeHistory = new HashSet<WalletHistoryRecord>();

			var transactions = await GetAllChainAndMemPoolTransactionsBySafeAccountAsync(account);
			var scriptPubKeys = await GetTrackedScriptPubKeysBySafeAccountAsync(account);

			foreach (SmartTransaction transaction in transactions)
			{
                WalletHistoryRecord record = new WalletHistoryRecord
                {
                    TransactionId = transaction.GetHash(),
                    BlockHeight = transaction.Height,

                    TimeStamp = !transaction.Confirmed
                    ? transaction.GetFirstSeenIfMemPoolHeight() ?? DateTimeOffset.UtcNow
                    : (await GetHeaderChainAsync()).GetBlock(transaction.Height).Header.BlockTime,

                    Amount = Money.Zero //for now
                };

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

		public async Task<HashSet<SmartTransaction>> GetAllChainAndMemPoolTransactionsBySafeAccountAsync(SafeAccount account = null)
		{
			HashSet<Script> trackedScriptPubkeys = await GetTrackedScriptPubKeysBySafeAccountAsync(account);
			var foundTransactions = new HashSet<SmartTransaction>();

			foreach (var spk in trackedScriptPubkeys)
			{
                var result = await TryFindAllChainAndMemPoolTransactionsAsync(spk);
                var rec = result.ReceivedTransactions;
                var spent = result.SpentTransactions;
                if (result.Success)
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

		public async Task<HashSet<Script>> GetTrackedScriptPubKeysBySafeAccountAsync(SafeAccount account = null)
        {
            var maxTracked = (await GetTrackerAsync()).TrackedScriptPubKeys.Count;
            var allPossiblyTrackedAddresses = new HashSet<BitcoinAddress>();

            var addressTypes = new HashSet<AddressType>
            {
                AddressType.Pay2PublicKeyHash,
                AddressType.Pay2WitnessPublicKeyHash
            };
            foreach (AddressType addressType in addressTypes)
            {
                foreach (var address in Safe.GetFirstNAddresses(addressType, maxTracked, HdPathType.Receive, account))
                {
                    allPossiblyTrackedAddresses.Add(address);
                }
                foreach (var address in Safe.GetFirstNAddresses(addressType, maxTracked, HdPathType.Change, account))
                {
                    allPossiblyTrackedAddresses.Add(address);
                }
                foreach (var address in Safe.GetFirstNAddresses(addressType, maxTracked, HdPathType.NonHardened, account))
                {
                    allPossiblyTrackedAddresses.Add(address);
                }
            }

            var actuallyTrackedScriptPubKeys = new HashSet<Script>();
            foreach (var address in allPossiblyTrackedAddresses)
            {
                if ((await GetTrackerAsync()).TrackedScriptPubKeys.Any(x => x == address.ScriptPubKey))
                    actuallyTrackedScriptPubKeys.Add(address.ScriptPubKey);
            }

            return actuallyTrackedScriptPubKeys;
        }

		/// <summary>
		///
		/// </summary>
		/// <param name="scriptPubKey"></param>
		/// <returns></returns>
		public async Task<(bool Success, HashSet<SmartTransaction> ReceivedTransactions, HashSet<SmartTransaction> SpentTransactions)> TryFindAllChainAndMemPoolTransactionsAsync(Script scriptPubKey)
		{
			var found = false;
			var receivedTransactions = new HashSet<SmartTransaction>();
			var spentTransactions = new HashSet<SmartTransaction>();

			foreach (var tx in (await GetTrackerAsync()).TrackedTransactions)
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
				foreach (var tx in (await GetTrackerAsync()).TrackedTransactions)
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

			return (found, receivedTransactions, spentTransactions);
		}

		public async Task<ChainedBlock> TryGetHeaderAsync(Height height)
		{
			ChainedBlock header = null;
			try
			{
				if (_connectionParameters == null)
					return null;

				header = (await GetHeaderChainAsync()).GetBlock(height);
			}
            catch (Exception)
            {
                header = null;
            }
            return header;
		}

		public async Task<(bool Success, Height Height)> TryGetHeaderHeightAsync()
		{
			var height = Height.Unknown;
			try
			{
				if (_connectionParameters == null)
					return (false, height);

				height = new Height((await GetHeaderChainAsync()).Height);
				return (true, height);
			}
            catch (Exception)
            {
                return (false, height);
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
						(await GetCreationHeightAsync()) == Height.Unknown || (await GetHeaderChainAsync()).Height < (await GetCreationHeightAsync()))
					{
						State = WalletState.SyncingHeaders;
						await Task.Delay(100, ctsToken).ContinueWith(tsk => { });
						continue;
					}

					Height height;
					Height trackerBestHeight = (await GetTrackerAsync()).BestHeight;
					var downloadMissing = false;
					if ((await GetTrackerAsync()).BlockCount == 0)
					{
						height = await GetCreationHeightAsync();
					}
					else if (trackerBestHeight.Type != HeightType.Chain)
					{
						await Task.Delay(100, ctsToken).ContinueWith(tsk => { });
						continue;
					}
					else if (_missingBlocks.Count() != 0)
					{
						downloadMissing = true;
						height = new Height(_missingBlocks.Min());
					}
					else
					{
						Height unprocessedBlockBestHeight = (await GetTrackerAsync()).UnprocessedBlockBuffer.BestHeight;
						// if no blocks to download (or process) start syncing mempool
						if ((await GetHeaderChainAsync()).Height <= trackerBestHeight)
						{
							State = MemPoolJob.SyncedOnce ? WalletState.Synced : WalletState.SyncingMemPool;
                            MemPoolJob.Enabled = true;
							await Task.Delay(100, ctsToken).ContinueWith(tsk => { });
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
									&& ((await GetHeaderChainAsync()).Height <= unprocessedBlockBestHeight)
								)
								|| (await GetTrackerAsync()).UnprocessedBlockBuffer.Full)
							{
								await Task.Delay(100, ctsToken).ContinueWith(tsk => { });
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
									await Task.Delay(100, ctsToken).ContinueWith(tsk => { });
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
						lookAheadHeight = new Height((await GetHeaderChainAsync()).Height);
					}

					var block = await BlockDownloader.TakeBlockAsync(firstHeight, lookAheadHeight, ctsToken);

					if (ctsToken.IsCancellationRequested) return;

					if (block == null)
					{
						// should not happen
						await Task.Delay(1000);
						continue;
					}

					if (ctsToken.IsCancellationRequested) return;

					// if the hash of the downloaded block is not the same as the header's
					// if the proof of work and merkle root isn't valid
					if ((await GetHeaderChainAsync()).GetBlock(height).HashBlock != block.GetHash()
						|| !block.Check())
					{
						await ReorgAsync();
						continue;
					}

                    (await GetTrackerAsync()).AddOrReplaceBlock(height, block);
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
		private async Task EnsureNoMissingRelevantBlocksAsync()
		{
			// must be this complicated for performance
			var neededHeights = new HashSet<int>();
			foreach (var h in (await GetTrackerAsync()).TrackedTransactions
				.Where(x => x.Height.Type == HeightType.Chain)
				.Select(x => x.Height))
			{
				neededHeights.Add(h.Value);
			}

			foreach (var h in (await GetTrackerAsync()).MerkleChain.Select(x => x.Height.Value))
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

		private async Task ReorgAsync()
		{
			BlockDownloader.Clear();
			(await GetHeaderChainAsync()).SetTip((await GetHeaderChainAsync()).Tip.Previous);
            (await GetTrackerAsync()).ReorgOne();
			await SaveAllChangedAsync();
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

					await SaveAllChangedAsync();

					await Task.Delay(delay, ctsToken).ContinueWith(tsk => { });
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
            using (await _asyncLockSave.LockAsync())
            {
				(GetAddressManager()).SavePeerFile(_addressManagerFilePath, Safe.Network);
				Debug.WriteLine($"Saved {nameof(AddressManager)}");

				if (_connectionParameters != null)
				{
					var headerHeight = new Height((await GetHeaderChainAsync()).Height);
					if (_savedHeaderHeight == Height.Unknown || headerHeight > _savedHeaderHeight)
					{
						await SaveHeaderChainAsync();
						Debug.WriteLine($"Saved HeaderChain at height: {headerHeight}");
						_savedHeaderHeight = headerHeight;
					}
				}
			}

			var bestHeight = await GetBestHeightAsync();
			var trackerBlockCount = (await GetTrackerAsync()).BlockCount;
			if (bestHeight.Type == HeightType.Chain
				&& (_savedTrackerBlockCount == -1
					|| trackerBlockCount > _savedTrackerBlockCount))
			{
				await (await GetTrackerAsync()).SaveAsync(_trackerFolderPath);
				Debug.WriteLine($"Saved {nameof(Tracker)} at height: {bestHeight} and block count: {trackerBlockCount}");
				_savedTrackerBlockCount = trackerBlockCount;
			}
		}

		private async Task SaveHeaderChainAsync()
		{
			using (var fs = File.Open(_headerChainFilePath, FileMode.Create))
			{
                (await GetHeaderChainAsync()).WriteTo(fs);
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
				Script changeScriptPubKey = (await GetUnusedScriptPubKeysAsync(AddressType.Pay2WitnessPublicKeyHash, account, HdPathType.Change)).FirstOrDefault();

				// 2. Find all coins I can spend from the account
				// 3. How much money we can spend?
				Debug.WriteLine("Calculating available amount...");
                var getBalanceResult = await GetBalanceAsync(account);
                var unspentCoins = getBalanceResult.UnspentCoins;
                AvailableAmount balance = getBalanceResult.Available;
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
                    using (var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(1)))
                    {
                        var feeRate = await FeeService.GetFeeRateAsync(feeType, cancellationTokenSource.Token);
                        feePerBytes = (feeRate.FeePerK / 1000);
                    }
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
					const int expectedMinTxSize = 1 * 41 + 1 * 33 + 10;
					try
					{
						inNum = SelectCoinsToSpend(unspentCoins, amount + feePerBytes * expectedMinTxSize).Count;
					}
					catch (InsufficientBalanceException)
					{
						return NotEnoughFundsBuildTransactionResult;
					}
				}

                // https://bitcoincore.org/en/segwit_wallet_dev/#transaction-fee-estimation
                // https://bitcoin.stackexchange.com/a/46379/26859
                int outNum = spendAll ? 1 : 2; // 1 address to send + 1 for change
				var origTxSize = inNum * 146 + outNum * 33 + 10;
                var newTxSize = inNum * 41 + outNum * 33 + 10; // BEWARE: This assumes segwit only inputs!
                var vSize = (int)Math.Ceiling(((3 * newTxSize) + origTxSize) / 4m);
                Debug.WriteLine($"Estimated tx size: {vSize} bytes");
				Money fee = feePerBytes * vSize;
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
                    var signingKey = Safe.FindPrivateKey(coin.ScriptPubKey.GetDestinationAddress(Safe.Network), (await GetTrackerAsync()).TrackedScriptPubKeys.Count, account);
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
                    .Shuffle()
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

		public async Task<IEnumerable<Script>> GetUnusedScriptPubKeysAsync(AddressType type, SafeAccount account = null, HdPathType hdPathType = HdPathType.Receive)
		{
			AssertAccount(account);

            var scriptPubKeys = new HashSet<Script>();
            int i = 0;
            while (true)
            {
                Script scriptPubkey = account == null ? Safe.GetAddress(type, i, hdPathType).ScriptPubKey : Safe.GetAddress(type, i, hdPathType, account).ScriptPubKey;
                if ((await GetTrackerAsync()).IsClean(scriptPubkey))
                {
                    scriptPubKeys.Add(scriptPubkey);
                    if (scriptPubKeys.Count >= MaxCleanAddressCount)
                        return scriptPubKeys;
                }
                i++;
            }
        }

		internal HashSet<Coin> SelectCoinsToSpend(IDictionary<Coin, bool> unspentCoins, Money totalOutAmount)
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
		private bool SelectCoins(ref HashSet<Coin> coinsToSpend, Money totalOutAmount, IEnumerable<Coin> unspentCoins)
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

		public async Task<(AvailableAmount Available, IDictionary<Coin, bool> UnspentCoins)> GetBalanceAsync(SafeAccount account = null)
		{
			// 1. Find all coins I can spend from the account
			Debug.WriteLine("Finding all unspent coins...");
			var unspentCoins = await GetUnspentCoinsAsync(account);

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

            return
                (new AvailableAmount
            {
                Confirmed = confirmedAvailableAmount,
                Unconfirmed = unconfirmedAvailableAmount
            },
            unspentCoins);

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
		public async Task<IDictionary<Coin, bool>> GetUnspentCoinsAsync(SafeAccount account = null)
		{
			AssertAccount(account);

			var unspentCoins = new Dictionary<Coin, bool>();

			var trackedScriptPubkeys = await GetTrackedScriptPubKeysBySafeAccountAsync(account);

			// 1. Go through all the transactions and their outputs
			foreach (SmartTransaction tx in (await GetTrackerAsync())
				.TrackedTransactions
				.Where(x => x.Height != Height.Unknown))
			{
				foreach (var coin in tx.Transaction.Outputs.AsCoins())
				{
					// 2. Check if the coin comes with our account
					if (trackedScriptPubkeys.Contains(coin.ScriptPubKey))
					{
						// 3. Check if coin is unspent, if so add to our utxoSet
						if (await IsUnspentAsync(coin))
						{
							unspentCoins.Add(coin, tx.Confirmed);
						}
					}
				}
			}

			return unspentCoins;
		}

		private async Task<bool> IsUnspentAsync(Coin coin) => (await GetTrackerAsync())
			.TrackedTransactions
			.Where(x => x.Height.Type == HeightType.Chain || x.Height.Type == HeightType.MemPool)
			.SelectMany(x => x.Transaction.Inputs)
			.All(txin => txin.PrevOut != coin.Outpoint);

		public async Task<SendTransactionResult> SendTransactionAsync(Transaction tx)
		{
			Debug.WriteLine($"Broadcasting Transaction: {tx.GetHash()}");

			if (State < WalletState.SyncingMemPool)
			{
				return new SendTransactionResult
				{
					Success = false,
					FailingReason =
						"Only propagate transactions after all blocks are downloaded"
				};
			}

			var failureResult = new SendTransactionResult
			{
				Success = false,
				FailingReason =
						$"The transaction might not have been successfully broadcasted. Check the Transaction ID in a block explorer. Transaction Id: {tx.GetHash()}"
			};

			var successfulResult = new SendTransactionResult
			{
				Success = true,
				FailingReason = ""
			};

			try
			{
				Debug.WriteLine($"Changing Tor circuit: {tx.GetHash()}");
				await ControlPortClient.ChangeCircuitAsync();
				
				await Task.Delay(100);
				await TorSmartBitClient.PushTransactionAsync(tx, CancellationToken.None);
				
				for (int i = 0; i < 21; i++)
				{
					await Task.Delay(1000);
					var arrived = MemPoolJob.Transactions.Contains(tx.GetHash());
					if (arrived)
					{
						Debug.WriteLine("Transaction is successfully propagated on the network.");
						return successfulResult;
					}
				}
			}
			catch(Exception ex)
			{
				failureResult.FailingReason += $" Details: {ex.ToString()}";
			}
			return failureResult;
		}

		public struct SendTransactionResult
		{
			public bool Success;
			public string FailingReason;
		}

		#endregion
	}
}
