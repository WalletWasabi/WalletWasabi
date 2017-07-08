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
using NBitcoin;
using System.Threading;

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
				var torPath = "tor"; // On Linux and OSX tor must be installed and added to path
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					torPath = @"tor\Tor\tor.exe";
				}
				var torProcessStartInfo = new ProcessStartInfo(torPath)
				{
					Arguments = Tor.TorArguments,
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardOutput = true
				};

				try
				{
					// if doesn't fail tor is already running with the control port
					Tor.ControlPortClient.IsCircuitEstabilishedAsync().Wait();
					Debug.WriteLine($"Tor is already running, using the existing instance.");
				}
				catch
				{
					Debug.WriteLine($"Starting Tor with arguments: {Tor.TorArguments}");
					try
					{
						Tor.TorProcess = Process.Start(torProcessStartInfo);
					}
					catch
					{
						// ignore, just run the torjob
					}
				}
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
				Tor.MakeSureCircuitEstabilishedAsync();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
				
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
