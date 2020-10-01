using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using WalletWasabi.Logging;

namespace WalletWasabi.Core
{
	public static class Program
	{
#pragma warning disable IDE1006 // Naming Styles

		public static async Task Main(string[] args)
#pragma warning restore IDE1006 // Naming Styles
		{
			try
			{
				using var host = CreateHostBuilder(args).Build();
				await host.RunWithTasksAsync();
			}
			catch (Exception ex)
			{
				Logger.LogCritical(ex);
			}
		}

		public static IHostBuilder CreateHostBuilder(string[] args)
		{
			return Host.CreateDefaultBuilder(args)
				.ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>());
		}
	}
}
