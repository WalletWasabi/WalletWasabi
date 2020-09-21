using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Backend
{
	public class Program
	{
		[SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "The Main method is the entry point of a C# application")]
		public static async Task Main(string[] args)
		{
			try
			{
				var endPoint = "http://localhost:37127/";

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
