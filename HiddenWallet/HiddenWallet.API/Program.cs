using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using HiddenWallet.API.Wrappers;
using System.Net.Http;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.DotNet.PlatformAbstractions;
using System.Runtime.InteropServices;

namespace HiddenWallet.API
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var endPoint = "http://localhost:37120/";
			var alreadyRunning = false;
			using (var client = new HttpClient())
			{
				try
				{
					client.GetAsync(endPoint + "api/v1/wallet/test").Wait();
					alreadyRunning = true;
				}
				catch
				{
					alreadyRunning = false;
				}
			}

			if (!alreadyRunning)
			{
				Console.WriteLine("Starting Tor process...");
				var torPath = @"tor\Tor\tor";
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					torPath = @"tor\Tor\tor.exe";
				}
				var torProcessStartInfo = new ProcessStartInfo(torPath)
				{
					Arguments = "SOCKSPort 37121 ControlPort 37122 HashedControlPassword 16:0978DBAF70EEB5C46063F3F6FD8CBC7A86DF70D2206916C1E2AE29EAF6",
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardOutput = true
				};
				Global.TorProcess = Process.Start(torProcessStartInfo);

				Global.WalletWrapper = new WalletWrapper();

				var host = new WebHostBuilder()
					.UseKestrel()
					.UseContentRoot(Directory.GetCurrentDirectory())
					.UseStartup<Startup>()
					.UseUrls(endPoint)
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
