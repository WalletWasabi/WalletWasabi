using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using HiddenWallet.API.Wrappers;
using System.Net.Http;
using System;

namespace HiddenWallet.API
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var alreadyRunning = false;
			using (var client = new HttpClient())
			{
				try
				{
					client.GetAsync("http://localhost:5000/api/v1/wallet/test").Wait();
					alreadyRunning = true;
				}
				catch
				{
					alreadyRunning = false;
				}
			}

			if (!alreadyRunning)
			{
				Global.WalletWrapper = new WalletWrapper();

				var host = new WebHostBuilder()
					.UseKestrel()
					.UseContentRoot(Directory.GetCurrentDirectory())
					.UseStartup<Startup>()
					.Build();

				host.Run();
			}
			else
			{
				Console.WriteLine("API is already running. Shutting down...");
			}
		}		
	}
}
