using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentCollections;
using MagicalCryptoWallet.Backend.Models;
using MagicalCryptoWallet.Helpers;
using MagicalCryptoWallet.KeyManagement;
using MagicalCryptoWallet.Logging;
using MagicalCryptoWallet.Models;
using NBitcoin;
using Nito.AsyncEx;

namespace MagicalCryptoWallet.Services
{
	public class WalletService
	{
		public KeyManager KeyManager { get; }
		public BlockDownloader BlockDownloader { get; }
		public IndexDownloader IndexDownloader { get; }

		public AsyncLock HandleFiltersLock { get; }

		/// <summary>
		/// 0: Not started, 1: Running, 2: Stopping, 3: Stopped
		/// </summary>
		private long _running;
		public bool IsRunning => Interlocked.Read(ref _running) == 1;
		public bool IsStopping => Interlocked.Read(ref _running) == 2;

		public WalletService(KeyManager keyManager, BlockDownloader blockDownloader, IndexDownloader indexDownloader)
		{
			KeyManager = Guard.NotNull(nameof(keyManager), keyManager);
			BlockDownloader = Guard.NotNull(nameof(blockDownloader), blockDownloader);
			IndexDownloader = Guard.NotNull(nameof(indexDownloader), indexDownloader);

			HandleFiltersLock = new AsyncLock();
			IndexDownloader.NewFilter += IndexDownloader_NewFilter;
			IndexDownloader.Reorged += IndexDownloader_Reorged;
		}

		private void IndexDownloader_Reorged(object sender, uint256 invalidBlockHash)
		{
			using (HandleFiltersLock.Lock())
			{
				BlockDownloader.TryRemove(invalidBlockHash);
				// ToDo: It must do more.
			}
		}

		private void IndexDownloader_NewFilter(object sender, FilterModel filterModel)
		{
			using (HandleFiltersLock.Lock())
			{
				if (filterModel.Filter != null)
				{
					var matchFound = filterModel.Filter.MatchAny(KeyManager.GetKeys().Select(x => x.GetP2wpkhScript().ToCompressedBytes()), filterModel.FilterKey);
					if (matchFound)
					{
						BlockDownloader.QueToDownload(filterModel.BlockHash);
					}
				}
			}
		}

		public void Initialize()
		{
			if (!BlockDownloader.IsRunning)
			{
				throw new NotSupportedException($"{nameof(BlockDownloader)} is not running.");
			}
			if (!IndexDownloader.IsRunning)
			{
				throw new NotSupportedException($"{nameof(IndexDownloader)} is not running.");
			}

			// Go through the filters and que to download the matches.
			var filters = IndexDownloader.GetFiltersIncluding(IndexDownloader.StartingFilter.BlockHeight);

			foreach (var filterModel in filters.Where(x => x.Filter != null)) // Filter can be null if there is no bech32 tx.
			{
				var matchFound = filterModel.Filter.MatchAny(KeyManager.GetKeys().Select(x => x.GetP2wpkhScript().ToCompressedBytes()), filterModel.FilterKey);
				if (matchFound)
				{
					BlockDownloader.QueToDownload(filterModel.BlockHash);
				}
			}
		}

		public void Synchronize()
		{
			Interlocked.Exchange(ref _running, 1);

			Task.Run(async () =>
			{
				try
				{
					while (IsRunning)
					{
						try
						{
							if (!BlockDownloader.IsRunning)
							{
								Logger.LogError<WalletService>($"{nameof(BlockDownloader)} is not running.");
								await Task.Delay(1000);
								continue;
							}
							if (!IndexDownloader.IsRunning)
							{
								Logger.LogError<WalletService>($"{nameof(IndexDownloader)} is not running.");
								await Task.Delay(1000);
								continue;
							}

							await Task.Delay(1000); // dummmy wait for now
						}
						catch (Exception ex)
						{
							Logger.LogDebug<WalletService>(ex);
						}
					}
				}
				finally
				{
					if (IsStopping)
					{
						Interlocked.Exchange(ref _running, 3);
					}
				}
			}
			);
		}

		public async Task StopAsync()
		{
			IndexDownloader.NewFilter -= IndexDownloader_NewFilter;
			IndexDownloader.Reorged -= IndexDownloader_Reorged;
			if (IsRunning)
			{
				Interlocked.Exchange(ref _running, 2);
			}
			while (IsStopping)
			{
				await Task.Delay(50);
			}
		}
	}
}
