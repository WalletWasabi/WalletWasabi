using Microsoft.Extensions.Hosting;
using NBitcoin;
using Nito.AsyncEx;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Services.Terminate;
using WalletWasabi.Stores;
using WalletWasabi.Wallets.FilterProcessor;

namespace WalletWasabi.Wallets;

public class WalletFilterProcessor : BackgroundService
{
	public WalletFilterProcessor(KeyManager keyManager, BitcoinStore bitcoinStore, TransactionProcessor transactionProcessor, BlockProvider blockProvider, EventBus eventBus)
	{
		_keyManager = keyManager;
		_bitcoinStore = bitcoinStore;
		_transactionProcessor = transactionProcessor;
		_blockProvider = blockProvider;
		_eventBus = eventBus;
		_blockFilterIterator = new(_bitcoinStore.IndexStore);
		_initialSynchronizationFinished = new TaskCompletionSource();
	}

	private readonly KeyManager _keyManager;
	private readonly BitcoinStore _bitcoinStore;
	private readonly TransactionProcessor _transactionProcessor;
	private readonly BlockProvider _blockProvider;
	private readonly EventBus _eventBus;
	private readonly BlockFilterIterator _blockFilterIterator;
	private readonly TaskCompletionSource _initialSynchronizationFinished;

	public Task InitialSynchronizationFinished => _initialSynchronizationFinished.Task;
	/// <summary>Make sure we don't process any request while a reorg is happening.</summary>
	private readonly AsyncLock _reorgLock = new();

	/// <inheritdoc />
	/// <summary>Used for filter synchronization.</summary>
	protected override async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				using (await _reorgLock.LockAsync(cancellationToken).ConfigureAwait(false))
				{
					Height lastHeight = _keyManager.GetBestHeight();

					if (lastHeight == _bitcoinStore.SmartHeaderChain.TipHeight)
					{
						_initialSynchronizationFinished.TrySetResult();
						await Task.Delay(1_000, cancellationToken).ConfigureAwait(false);
						continue;
					}

					uint currentHeight = (uint)lastHeight.Value + 1;
					FilterModel filter = await _blockFilterIterator.GetAndRemoveAsync(currentHeight, cancellationToken).ConfigureAwait(false);
					var matchFound = await ProcessFilterModelAsync(filter, cancellationToken).ConfigureAwait(false);
					_eventBus.Publish(new FilterProcessed(filter));

					var reachedBlockChainTip = currentHeight == _bitcoinStore.SmartHeaderChain.TipHeight;
					bool storeToDisk = matchFound || reachedBlockChainTip;
					_keyManager.SetBestHeight(new Height(currentHeight), storeToDisk);
				}
			}
		}
		catch (OperationCanceledException)
		{
			Logger.LogDebug("Filter processor's execution was stopped.");
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			TerminateService.Instance?.SignalGracefulCrash(ex);
			throw;
		}
	}

	/// <summary>
	/// Return the keys to test against the filter depending on the height of the filter and the type of synchronization.
	/// </summary>
	/// <param name="isBip158"></param>
	/// <returns>Keys to test against this filter.</returns>
	private IEnumerable<byte[]> GetScriptPubKeysToTest(bool isBip158)
	{
		// Wasabi doesn't build bip158 filters and also uses the compact representation of the scriptPubKeys
		return _keyManager.UnsafeGetSynchronizationInfos(isBip158);
	}

	private async Task<bool> ProcessFilterModelAsync(FilterModel filter, CancellationToken cancel)
	{
		var height = new Height(filter.Header.Height);

		var toTestKeys = GetScriptPubKeysToTest(filter.Filter.IsBip158());

		var matchFound = false;
		if (toTestKeys.Any())
		{
			var compressedScriptPubKeys = toTestKeys;
			matchFound = filter.Filter.MatchAny(compressedScriptPubKeys, filter.FilterKey);

			if (matchFound)
			{
				// Wait until downloaded.
				var currentBlock = await _blockProvider(filter.Header.BlockHash, cancel).ConfigureAwait(false);
				var txsToProcess = new List<SmartTransaction>();
				for (int i = 0; i < currentBlock.Transactions.Count; i++)
				{
					Transaction tx = currentBlock.Transactions[i];
					txsToProcess.Add(new SmartTransaction(tx, height, currentBlock.GetHash(), i, firstSeen: currentBlock.Header.BlockTime, labels: _bitcoinStore.MempoolService.TryGetLabel(tx.GetHash())));
				}

				_transactionProcessor.Process(txsToProcess);
			}
		}
		return matchFound;
	}

	private async void ReorgedAsync(object? sender, FilterModel invalidFilter)
	{
		try
		{
			uint256 invalidBlockHash = invalidFilter.Header.BlockHash;
			uint newBestHeight = invalidFilter.Header.Height - 1;

			using (await _reorgLock.LockAsync(CancellationToken.None).ConfigureAwait(false))
			{
				_keyManager.SetMaxBestHeight(new Height(newBestHeight));
				_transactionProcessor.UndoBlock((int)invalidFilter.Header.Height);
				_bitcoinStore.TransactionStore.ReleaseToMempoolFromBlock(invalidBlockHash);
				_blockFilterIterator.RemoveNewerThan(newBestHeight);
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex);
		}
	}


	public override async Task StartAsync(CancellationToken cancellationToken)
	{
		_bitcoinStore.IndexStore.Reorged += ReorgedAsync;
		await base.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		_bitcoinStore.IndexStore.Reorged -= ReorgedAsync;
		await base.StopAsync(cancellationToken).ConfigureAwait(false);
	}
}
