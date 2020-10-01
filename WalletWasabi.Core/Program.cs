using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using WalletWasabi.Logging;

namespace WalletWasabi.Core
{
	public class Program
	{
#pragma warning disable IDE1006 // Naming Styles

		public static async Task Main(string[] args)
#pragma warning restore IDE1006 // Naming Styles
		{
			try
			{
				Logger.LogInfo("Hello Core");
				var endPoint = "http://localhost:37129/";

				using var host = Host.CreateDefaultBuilder(args)
					.ConfigureWebHostDefaults(webBuilder => webBuilder
							.UseStartup<Startup>()
							.UseUrls(endPoint))
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
