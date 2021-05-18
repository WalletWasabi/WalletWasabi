using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Integration
{
	// BackgroundServiceStarter is a IHostedService whose only responsibility is to start/stop other service.
	// It provides the most ellegant way to be able to register a singleton hosted service. ASPNET provides
	// service.AddHostedService() method but it registers it as transient instead of singleton and for that
	// reason the instance is different for every http request.
	//
	// see: https://stackoverflow.com/a/51314147/627071
	public class BackgroundServiceStarter<T> : IHostedService where T : IHostedService
	{
		private readonly T backgroundService;

		public BackgroundServiceStarter(T backgroundService)
		{
			this.backgroundService = backgroundService;
		}

		public Task StartAsync(CancellationToken cancellationToken)
		{
			return backgroundService.StartAsync(cancellationToken);
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			return backgroundService.StopAsync(cancellationToken);
		}
	}
}
