using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Monitoring;
using WalletWasabi.Logging;

namespace WalletWasabi.Services
{
	public class HostedServices : IDisposable
	{
		private List<HostedService> Services { get; } = new List<HostedService>();

		private object ServicesLock { get; } = new object();
		private bool IsStartAllAsyncStarted { get; set; } = false;
		private bool IsStartAllAsyncCompleted { get; set; } = false;

		public void Register(IHostedService service, string friendlyName)
		{
			if (IsStartAllAsyncStarted)
			{
				throw new InvalidOperationException("Services are already started.");
			}

			lock (ServicesLock)
			{
				Services.Add(new HostedService(service, friendlyName));
			}
		}

		public async Task StartAllAsync(CancellationToken token)
		{
			if (IsStartAllAsyncStarted)
			{
				throw new InvalidOperationException("Operation is already started.");
			}
			IsStartAllAsyncStarted = true;

			var exceptions = new List<Exception>();
			var exceptionsLock = new object();

			var tasks = CloneServices().Select(x => x.Service.StartAsync(token).ContinueWith(y =>
			{
				if (y.Exception is null)
				{
					Logger.LogInfo($"Started {x.FriendlyName}.");
				}
				else
				{
					lock (exceptionsLock)
					{
						exceptions.Add(y.Exception);
					}
					Logger.LogError($"Error starting {x.FriendlyName}.");
					Logger.LogError(y.Exception);
				}
			}));

			await Task.WhenAll(tasks).ConfigureAwait(false);

			if (exceptions.Any())
			{
				throw new AggregateException(exceptions);
			}

			IsStartAllAsyncCompleted = true;
		}

		public async Task StopAllAsync(CancellationToken token)
		{
			var tasks = CloneServices().Select(x => x.Service.StopAsync(token).ContinueWith(y =>
			{
				if (y.Exception is null)
				{
					Logger.LogInfo($"Stopped {x.FriendlyName}.");
				}
				else
				{
					Logger.LogError($"Error stopping {x.FriendlyName}.");
					Logger.LogError(y.Exception);
				}
			}));

			await Task.WhenAll(tasks).ConfigureAwait(false);
		}

		private IEnumerable<HostedService> CloneServices()
		{
			lock (ServicesLock)
			{
				return Services.ToArray();
			}
		}

		public T FirstOrDefault<T>() where T : class
		{
			if (!IsStartAllAsyncCompleted)
			{
				throw new InvalidOperationException("Services are not yet started.");
			}
			lock (ServicesLock)
			{
				var found = Services.FirstOrDefault(x => x.Service is T);
				if (found?.Service is null)
				{
					return default;
				}
				return found.Service as T;
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
					foreach (var service in CloneServices())
					{
						if (service.Service is IDisposable disposable)
						{
							disposable?.Dispose();
							Logger.LogInfo($"Disposed {service.FriendlyName}.");
						}
					}
				}

				_disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
