using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Backend
{
	public static class Program
	{
#pragma warning disable IDE1006 // Naming Styles

		public static async Task Main(string[] args)
#pragma warning restore IDE1006 // Naming Styles
		{
			try
			{
				using var host = Host.CreateDefaultBuilder(args)
					.ConfigureWebHostDefaults(webBuilder => webBuilder
							.UseStartup<Startup>())
					.Build();

				await host.RunWithTasksAsync();
			}
			catch (Exception ex)
			{
				Logger.LogCritical(ex);
			}
		}
	}
}
