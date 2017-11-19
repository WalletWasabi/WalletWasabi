using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading;
using NBitcoin;
using HiddenWallet.ChaumianTumbler.Configuration;
using NBitcoin.RPC;
using System.Net;
using HiddenWallet.ChaumianTumbler.Store;
using System.Text;
using HiddenWallet.Crypto;
using HiddenWallet.ChaumianTumbler.Referee;

namespace HiddenWallet.ChaumianTumbler
{
	public class Program
	{
#pragma warning disable IDE1006 // Naming Styles
		public static async Task Main(string[] args)
#pragma warning restore IDE1006 // Naming Styles
		{
			try
			{
				await Global.InitializeAsync();
				
				using (var host = WebHost.CreateDefaultBuilder(args)
					.UseStartup<Startup>()
					.UseKestrel(options =>
					{
						// listen to requests from outside the local machine   
						options.Listen(IPAddress.Any, 80);
					})
					.Build())
				{
					await host.RunAsync();
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
				Console.WriteLine("Press a key to exit...");
				Console.ReadKey();
			}
		}
	}
}
