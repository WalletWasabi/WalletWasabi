using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Backend
{
	public static class Extensions
	{
		public static IServiceCollection AddStartupTask<T>(this IServiceCollection services)
			where T : class, IStartupTask
			=> services.AddTransient<IStartupTask, T>();

		public static async Task RunWithTasksAsync(this IHost host, CancellationToken cancellationToken = default)
		{
			await Task.WhenAll(host.Services.GetServices<IStartupTask>().Select(t => t.ExecuteAsync(cancellationToken)));
			await host.RunAsync(cancellationToken);
		}
	}
}
