using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Logging;

namespace WalletWasabi.Services
{
	public class HwiService
	{
		private long _running;

		public bool IsRunning => Interlocked.Read(ref _running) == 1;

		public event EventHandler<IEnumerable<HardwareWalletInfo>> NewEnumeration;

		private CancellationTokenSource Cancel { get; set; }

		public HwiService()
		{
			_running = 0;
		}

		public void Start()
		{
			if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
			{
				return;
			}

			Cancel = new CancellationTokenSource();
			Task.Run(async () =>
			{
				try
				{
					int waitTime = 3000;
					while (IsRunning)
					{
						try
						{
							if (Cancel.IsCancellationRequested)
							{
								break;
							}
							IEnumerable<HardwareWalletInfo> hwis = await HwiProcessManager.EnumerateAsync();
							waitTime = hwis.Any() ? 7000 : 3000;

							OnNewEnumeration(hwis);
						}
						catch (Exception ex)
						{
							Logger.LogTrace<HwiService>(ex);
						}
						finally
						{
							await Task.Delay(waitTime, Cancel.Token);
						}
					}
				}
				finally
				{
					Interlocked.CompareExchange(ref _running, 3, 2); // If IsStopping, make it stopped.
				}
			});
		}

		private void OnNewEnumeration(IEnumerable<HardwareWalletInfo> e)
		{
			NewEnumeration?.Invoke(this, e);
		}

		public async Task StopAsync()
		{
			Interlocked.CompareExchange(ref _running, 2, 1); // If running, make it stopping.
			Cancel?.Cancel();
			while (Interlocked.CompareExchange(ref _running, 3, 0) == 2)
			{
				await Task.Delay(50);
			}

			Cancel?.Dispose();
			Cancel = null;
		}
	}
}
