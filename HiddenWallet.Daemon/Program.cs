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
				rsaPath = Path.Combine(FullSpvWallet.Global.DataDir, "RsaPubKeyTestNet.json");
			}
			else
			{
				rsaPath = Path.Combine(FullSpvWallet.Global.DataDir, "RsaPubKeyMain.json");
			}
			if (File.Exists(rsaPath))
			{
				string rsaPubKeyJson = await File.ReadAllTextAsync(rsaPath, Encoding.UTF8);
				Global.RsaPubKey = BlindingRsaPubKey.CreateFromJson(rsaPubKeyJson);
			}
			else
			{
				// TODO: change these default values to the default testnet and mainnet server's keys
				var modulus = new BigInteger("23589394558000769094389882515724018755668807942927294461300128316443908394269505275903255579660647420728395606511481595615274221757680896183302901501925850296754719819863284461202461933453035616403571429927625259423834885626633643917138260424572248721156753288868407467908326062046250434581856159002739075698923536178721479169980865352795356860391627279931218179034394535213633628061061525586225808965225547470074014895411041599390841768961511372955835648824597673786292957294962958948903155018531955318632198851755013447911244491178476628417300289292303120213670519763639881558155258131869186859524584048386096963449");
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
