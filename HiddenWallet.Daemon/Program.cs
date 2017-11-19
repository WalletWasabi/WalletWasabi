using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using HiddenWallet.Daemon.Wrappers;
using System.Net.Http;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.DotNet.PlatformAbstractions;
using System.Runtime.InteropServices;
using NBitcoin;
using System.Threading;
using System.Text;
using HiddenWallet.Crypto;
using Org.BouncyCastle.Math;

namespace HiddenWallet.Daemon
{
	public class Program
	{
#pragma warning disable IDE1006 // Naming Styles
		public static async Task Main(string[] args)
#pragma warning restore IDE1006 // Naming Styles
		{
			var configFilePath = Path.Combine(FullSpvWallet.Global.DataDir, "Config.json");
			Global.Config = new Config();
			await Global.Config.LoadOrCreateDefaultFileAsync(configFilePath, CancellationToken.None);

			string rsaPath;
			if (Global.Config.Network == Network.Main)
			{
				rsaPath = Path.Combine(FullSpvWallet.Global.DataDir, "RsaPubKeyMain.json");
			}
			else
			{
				rsaPath = Path.Combine(FullSpvWallet.Global.DataDir, "RsaPubKeyTestNet.json");
			}
			if (File.Exists(rsaPath))
			{
				string rsaPubKeyJson = await File.ReadAllTextAsync(rsaPath, Encoding.UTF8);
				Global.RsaPubKey = BlindingRsaPubKey.CreateFromJson(rsaPubKeyJson);
			}
			else
			{
				// TODO: change these default values to the default testnet and mainnet server's keys
				var modulus = new BigInteger("23765292524202909590312633661559508105372542245888884022732405845352867619510400148700590630819909854245443768683013002454300998561366066863075543900854183218255556958603829744574143217473373221883959779439395079947766381668459828953701726054394843911847063235903761918014727346563249795000296937701840334243492972569641205615480654015909880231725449975599905030093854844840930446509242634799081158992191062980583060919718404098183279802755923836822248873360647411342760279712271227024615021525054883324946175934075264032794034681653369283673040524060792800105336875314739176592340110027306283977215862005685674572993");
				var exponent = new BigInteger("65537");
				Global.RsaPubKey = new BlindingRsaPubKey(modulus, exponent);
				await File.WriteAllTextAsync(rsaPath, Global.RsaPubKey.ToJson(), Encoding.UTF8);
				Console.WriteLine($"Created RSA key at: {rsaPath}");
			}

			var endPoint = "http://localhost:37120/";
			var alreadyRunning = false;
			using (var client = new HttpClient())
			{
				try
				{
					await client.GetAsync(endPoint + "api/v1/wallet/test");
					alreadyRunning = true;
				}
                catch (Exception)
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
					await Tor.ControlPortClient.IsCircuitEstabilishedAsync(); // ToDo fix typo in DotNetTor: estabilish -> establish
					Debug.WriteLine($"Tor is already running, using the existing instance.");
				}
                catch (Exception)
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

				await host.RunAsync();
			}
			else
			{
				Console.WriteLine("API is already running. Shutting down...");
			}
		}
	}
}
