using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MagicalCryptoWallet.Helpers;
using MagicalCryptoWallet.KeyManagement;
using MagicalCryptoWallet.Logging;

namespace MagicalCryptoWallet.Services
{
	public class WalletService
	{
		public KeyManager KeyManager { get; }
		public BlockDownloader BlockDownloader { get; }
		public IndexDownloader IndexDownloader { get; }

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

							await Task.Delay(10); // Dummy await for now.
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
			Interlocked.Exchange(ref _running, 2);
			while (IsStopping)
			{
				await Task.Delay(50);
			}
		}
	}
}
