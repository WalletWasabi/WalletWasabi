using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Indexer;

public static class Program
{
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
			.UseUrls(Environment.GetEnvironmentVariable("WASABI_BIND") ?? "http://localhost:37127/"));
}
