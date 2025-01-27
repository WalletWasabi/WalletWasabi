using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace WalletWasabi.Coordinator;

public static class Extensions
{
	public static IServiceCollection AddStartupTask<T>(this IServiceCollection services)
		where T : class
		=> services.AddTransient<StartupTask>();

	public static async Task RunWithTasksAsync(this IHost host, CancellationToken cancellationToken = default)
	{
		await Task.WhenAll(host.Services.GetServices<StartupTask>().Select(t => t.ExecuteAsync(cancellationToken)));
		await host.RunAsync(cancellationToken);
	}
}
