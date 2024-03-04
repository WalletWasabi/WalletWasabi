using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;
using WalletWasabi.Services.Terminate;

namespace WalletWasabi.Services;

public class HostedServices : IDisposable
{
	private volatile bool _disposedValue = false; // To detect redundant calls

	private List<HostedService> Services { get; } = new();

	private object ServicesLock { get; } = new();
	private bool IsStartAllAsyncStarted { get; set; } = false;

	public HostedServices(ITerminateService? terminateService = null)
	{
		TerminateService = terminateService;
	}

	private ITerminateService? TerminateService { get; }

	public void Register<T>(Func<IHostedService> serviceFactory, string friendlyName, bool terminateAppOnServiceCrash = false) where T : class, IHostedService
	{
		Register<T>(serviceFactory(), friendlyName, terminateAppOnServiceCrash);
	}

	private void Register<T>(IHostedService service, string friendlyName, bool terminateAppOnServiceCrash = false) where T : class, IHostedService
	{
		if (typeof(T) != service.GetType())
		{
			throw new ArgumentException($"Type mismatch: {nameof(T)} is {typeof(T).Name}, but {nameof(service)} is {service.GetType()}.");
		}

		if (IsStartAllAsyncStarted)
		{
			throw new InvalidOperationException("Services are already started.");
		}

		lock (ServicesLock)
		{
			if (AnyNoLock<T>())
			{
				throw new InvalidOperationException($"{typeof(T).Name} is already registered.");
			}
			Services.Add(new HostedService(service, friendlyName, terminateAppOnServiceCrash));
		}
	}

	public async Task StartAllAsync(CancellationToken token = default)
	{
		if (IsStartAllAsyncStarted)
		{
			throw new InvalidOperationException("Operation is already started.");
		}
		IsStartAllAsyncStarted = true;

		var exceptions = new List<Exception>();
		var exceptionsLock = new object();

		var tasks = CloneServices().Select(x =>
		{
			Task startTask;

			if (x.TerminateAppOnServiceCrash)
			{
				if (x.Service is BackgroundService backgroundService)
				{
					startTask = backgroundService.StartAndSetUpUnhandledExceptionCallbackAsync(TerminateService, token);
				}
				else
				{
					throw new InvalidOperationException($"Service '{x.Service}' is not a background service to register a crash callback.");
				}
			}
			else
			{
				startTask = x.Service.StartAsync(token);
			}

			return startTask.ContinueWith(y =>
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
					});
		});

		await Task.WhenAll(tasks).ConfigureAwait(false);

		if (exceptions.Count != 0)
		{
			throw new AggregateException(exceptions);
		}
	}

	/// <remarks>This method does not throw exceptions.</remarks>
	public async Task StopAllAsync(CancellationToken token = default)
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

	public T? GetOrDefault<T>() where T : class, IHostedService
	{
		lock (ServicesLock)
		{
			return Services.SingleOrDefault(x => x.Service is T)?.Service as T;
		}
	}

	public T Get<T>() where T : class, IHostedService
	{
		lock (ServicesLock)
		{
			return (T)Services.Single(x => x.Service is T).Service;
		}
	}

	public bool Any<T>() where T : class, IHostedService
	{
		lock (ServicesLock)
		{
			return AnyNoLock<T>();
		}
	}

	private bool AnyNoLock<T>() where T : class, IHostedService => Services.Any(x => x.Service is T);

	#region IDisposable Support

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
