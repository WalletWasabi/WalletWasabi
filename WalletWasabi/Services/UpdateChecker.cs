using System;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Exceptions;
using WalletWasabi.Logging;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Services
{
	public class UpdateChecker : IDisposable
	{
		/// <summary>
		/// 0: Not started, 1: Running, 2: Stopping, 3: Stopped
		/// </summary>
		private long _running;

		public bool IsRunning => Interlocked.Read(ref _running) == 1;
		public bool IsStopping => Interlocked.Read(ref _running) == 2;
		public bool IsStopped => Interlocked.Read(ref _running) == 3;

		private CancellationTokenSource Stop { get; }

		public WasabiClient WasabiClient { get; }

		public UpdateChecker(WasabiClient client)
		{
			WasabiClient = client;
			_running = 0;
			Stop = new CancellationTokenSource();
		}

		public void Start(TimeSpan period, Func<Task> executeIfBackendIncompatible, Func<Task> executeIfClientOutOfDate)
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
							(bool backendCompatible, bool clientUpToDate) updates = await WasabiClient.CheckUpdatesAsync(Stop.Token);

							if (!updates.backendCompatible)
							{
								await executeIfBackendIncompatible?.Invoke();
							}

							if (!updates.clientUpToDate)
							{
								await executeIfClientOutOfDate?.Invoke();
							}

							await Task.Delay(period, Stop.Token);
						}
						catch (ConnectionException ex)
						{
							Logger.LogError<UpdateChecker>(ex);
							try
							{
								await Task.Delay(period, Stop.Token); // Give other threads time to do stuff, update check is not crucial.
							}
							catch (TaskCanceledException ex2)
							{
								Logger.LogTrace<UpdateChecker>(ex2);
							}
						}
						catch (Exception ex) when (ex is OperationCanceledException
												|| ex is TaskCanceledException
												|| ex is TimeoutException)
						{
							Logger.LogTrace<UpdateChecker>(ex);
						}
						catch (Exception ex)
						{
							Logger.LogDebug<UpdateChecker>(ex);
						}
					}
				}
				finally
				{
					Interlocked.CompareExchange(ref _running, 3, 2); // If IsStopping, make it stopped.
				}
			});
		}

		public async Task StopAsync()
		{
			Interlocked.CompareExchange(ref _running, 2, 1); // If running, make it stopping.
			Stop?.Cancel();
			while (!IsStopped)
			{
				await Task.Delay(50);
			}
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					if (!IsStopped)
					{
						Interlocked.CompareExchange(ref _running, 2, 1); // If running, make it stopping.
						Stop?.Cancel();
						while (IsStopping)
						{
							Task.Delay(50).GetAwaiter().GetResult();
						}
					}
					Stop?.Dispose();
				}

				_disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// GC.SuppressFinalize(this);
		}

		#endregion IDisposable Support
	}
}
