using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
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
		public static async Task RunWithTasksAsync(this IWebHost webHost, CancellationToken cancellationToken = default)
		{
			// Load all tasks from DI
			var startupTasks = webHost.Services.GetServices<IStartupTask>();

			// Execute all the tasks
			foreach (var startupTask in startupTasks)
			{
				await startupTask.ExecuteAsync(cancellationToken);
			}

			// Start the tasks as normal
			await webHost.RunAsync(cancellationToken);
		}
	}
}
