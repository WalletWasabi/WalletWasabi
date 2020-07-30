using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.BitcoinCore
{
	public class CachedRpcClient : RpcClientBase
	{
		private CancellationTokenSource _tipChangeCancellationTokenSource;

		public CachedRpcClient(RPCClient rpc, IMemoryCache cache)
			: base(rpc)
		{
			Cache = cache;
		}

		private object CancellationTokenSourceLock { get; } = new object();

		public IMemoryCache Cache { get; }

		private CancellationTokenSource TipChangeCancellationTokenSource
		{
			get
			{
				lock (CancellationTokenSourceLock)
				{
					if (_tipChangeCancellationTokenSource is null || _tipChangeCancellationTokenSource.IsCancellationRequested)
					{
						_tipChangeCancellationTokenSource = new CancellationTokenSource();
					}
				}
				return _tipChangeCancellationTokenSource;
			}
		}

		public override async Task<uint256> GetBestBlockHashAsync()
		{
			string cacheKey = nameof(GetBestBlockHashAsync);

			return await Cache.AtomicGetOrCreateAsync(
				cacheKey,
				entry =>
				{
					entry.SetSize(1);
					entry.SetAbsoluteExpiration(TimeSpan.FromSeconds(4)); // The best hash doesn't change so often so, keep in cache for 4 seconds.
					entry.AddExpirationToken(new CancellationChangeToken(TipChangeCancellationTokenSource.Token));

					return base.GetBestBlockHashAsync();
				}).ConfigureAwait(false);
		}

		public override async Task<Block> GetBlockAsync(uint256 blockHash)
		{
			string cacheKey = $"{nameof(GetBlockAsync)}:{blockHash}";
			return await Cache.AtomicGetOrCreateAsync(
				cacheKey,
				entry =>
				{
					entry.SetSize(10);
					entry.SetAbsoluteExpiration(TimeSpan.FromSeconds(4)); // There is a block every 10 minutes in average so, keep in cache for 4 seconds.

					return base.GetBlockAsync(blockHash);
				}).ConfigureAwait(false);
		}

		public override async Task<Block> GetBlockAsync(uint blockHeight)
		{
			string cacheKey = $"{nameof(GetBlockAsync)}:{blockHeight}";
			return await Cache.AtomicGetOrCreateAsync(
				cacheKey,
				entry =>
				{
					entry.SetSize(10);
					entry.SetAbsoluteExpiration(TimeSpan.FromSeconds(4)); // There is a block every 10 minutes in average so, keep in cache for 4 seconds.

					return base.GetBlockAsync(blockHeight);
				}).ConfigureAwait(false);
		}

		public override async Task<BlockHeader> GetBlockHeaderAsync(uint256 blockHash)
		{
			string cacheKey = $"{nameof(GetBlockHeaderAsync)}:{blockHash}";
			return await Cache.AtomicGetOrCreateAsync(
				cacheKey,
				entry =>
				{
					entry.SetSize(2);
					entry.SetAbsoluteExpiration(TimeSpan.FromSeconds(4)); // There is a block every 10 minutes in average so, keep in cache for 4 seconds.

					return base.GetBlockHeaderAsync(blockHash);
				}).ConfigureAwait(false);
		}

		public override async Task<int> GetBlockCountAsync()
		{
			string cacheKey = nameof(GetBlockCountAsync);
			return await Cache.AtomicGetOrCreateAsync(
				cacheKey,
				entry =>
				{
					entry.SetSize(1);
					entry.SetAbsoluteExpiration(TimeSpan.FromSeconds(2)); // The blockchain info does not change frequently.
					entry.AddExpirationToken(new CancellationChangeToken(TipChangeCancellationTokenSource.Token));

					return base.GetBlockCountAsync();
				}).ConfigureAwait(false);
		}

		public override async Task<PeerInfo[]> GetPeersInfoAsync()
		{
			string cacheKey = nameof(GetPeersInfoAsync);
			return await Cache.AtomicGetOrCreateAsync(
				cacheKey,
				entry =>
				{
					entry.SetSize(2);
					entry.SetAbsoluteExpiration(TimeSpan.FromSeconds(0.5));

					return base.GetPeersInfoAsync();
				}).ConfigureAwait(false);
		}

		public override async Task<MempoolEntry> GetMempoolEntryAsync(uint256 txid, bool throwIfNotFound = true)
		{
			string cacheKey = $"{nameof(GetMempoolEntryAsync)}:{txid}";
			return await Cache.AtomicGetOrCreateAsync(
				cacheKey,
				entry =>
				{
					entry.SetSize(20);
					entry.SetAbsoluteExpiration(TimeSpan.FromSeconds(2));
					entry.AddExpirationToken(new CancellationChangeToken(TipChangeCancellationTokenSource.Token));

					return base.GetMempoolEntryAsync(txid, throwIfNotFound);
				}).ConfigureAwait(false);
		}

		public override async Task<uint256[]> GetRawMempoolAsync()
		{
			string cacheKey = nameof(GetRawMempoolAsync);
			return await Cache.AtomicGetOrCreateAsync(
				cacheKey,
				entry =>
				{
					entry.SetSize(20);
					entry.SetAbsoluteExpiration(TimeSpan.FromSeconds(2));
					entry.AddExpirationToken(new CancellationChangeToken(TipChangeCancellationTokenSource.Token));

					return base.GetRawMempoolAsync();
				}).ConfigureAwait(false);
		}

		public override async Task<GetTxOutResponse> GetTxOutAsync(uint256 txid, int index, bool includeMempool = true)
		{
			string cacheKey = $"{nameof(GetTxOutAsync)}:{txid}:{index}:{includeMempool}";
			return await Cache.AtomicGetOrCreateAsync(
				cacheKey,
				entry =>
				{
					entry.SetSize(2);
					entry.SetAbsoluteExpiration(TimeSpan.FromSeconds(2));

					return base.GetTxOutAsync(txid, index, includeMempool);
				}).ConfigureAwait(false);
		}

		public override async Task<uint256[]> GenerateAsync(int blockCount)
		{
			TipChangeCancellationTokenSource.Cancel();
			return await base.GenerateAsync(blockCount).ConfigureAwait(false);
		}

		public override async Task InvalidateBlockAsync(uint256 blockHash)
		{
			TipChangeCancellationTokenSource.Cancel();
			await base.InvalidateBlockAsync(blockHash);
		}
	}
}
