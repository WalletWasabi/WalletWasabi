using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MagicalCryptoWallet.Logging;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace MagicalCryptoWallet.Backend
{
    public class Program
    {
#pragma warning disable IDE1006 // Naming Styles
		public static async Task Main(string[] args)
#pragma warning restore IDE1006 // Naming Styles
		{
			try
			{
				Logger.SetFilePath(Path.Combine(Global.DataDir, "Logs.txt"));
				Logger.SetMinimumLevel(LogLevel.Info);
				Logger.SetModes(LogMode.Debug, LogMode.Console, LogMode.File);

				await Global.InitializeAsync();

				var endPoint = "http://localhost:37127/";

				using (var host = WebHost.CreateDefaultBuilder(args)
					.UseStartup<Startup>()
					.UseUrls(endPoint)
					.Build())
				{
					await host.RunAsync();
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning<Program>(ex);
			}
		}
    }
}
