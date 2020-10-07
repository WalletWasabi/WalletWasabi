using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Backend
{
	public static class Program
	{
		[SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "The Main method is the entry point of a C# application")]
		public static async Task Main(string[] args)
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

		public static IHostBuilder CreateHostBuilder(string[] args) =>
			Host.CreateDefaultBuilder(args).ConfigureWebHostDefaults(webBuilder => webBuilder
				.UseStartup<Startup>()
				.UseUrls("http://localhost:37127/"));
	}
}
