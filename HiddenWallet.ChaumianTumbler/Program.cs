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
				string configFilePath = Path.Combine(Global.DataDir, "Config.json");
				Global.Config = new Config();
				await Global.Config.LoadOrCreateDefaultFileAsync(configFilePath, CancellationToken.None);

				string rsaPath = Path.Combine(Global.DataDir, "RsaKey.json");
				if (File.Exists(rsaPath))
				{
					string rsaKeyJson = await File.ReadAllTextAsync(rsaPath, Encoding.UTF8);
					Global.RsaKey = BlindingRsaKey.CreateFromJson(rsaKeyJson);
				}
				else
				{
					Global.RsaKey = new BlindingRsaKey();
					await File.WriteAllTextAsync(rsaPath, Global.RsaKey.ToJson(), Encoding.UTF8);
					Console.WriteLine($"Created RSA key at: {rsaPath}");
				}

				Global.RpcClient = new RPCClient(
					credentials: new RPCCredentialString
					{
						UserPassword = new NetworkCredential(Global.Config.BitcoinRpcUser, Global.Config.BitcoinRpcPassword)
					},
					network: Global.Config.Network);
				await AssertRpcNodeFullyInitializedAsync();
				
				if(File.Exists(Global.CoinJoinStorePath))
				{
					Global.CoinJoinStore = await CoinJoinStore.CreateFromFileAsync(Global.CoinJoinStorePath);
				}
				else
				{
					Global.CoinJoinStore = new CoinJoinStore();
				}

				if (File.Exists(Global.UtxoRefereePath))
				{
					Global.UtxoReferee = await UtxoReferee.CreateFromFileAsync(Global.UtxoRefereePath);
				}
				else
				{
					Global.UtxoReferee = new UtxoReferee();
				}
				Global.UtxoRefereeJobCancel = new CancellationTokenSource();
				Global.UtxoRefereeJob = Global.UtxoReferee.StartAsync(Global.UtxoRefereeJobCancel.Token);

				Global.StateMachine = new TumblerStateMachine();
				Global.StateMachineJobCancel = new CancellationTokenSource();
				Global.StateMachineJob = Global.StateMachine.StartAsync(Global.StateMachineJobCancel.Token);

				using (var host = WebHost.CreateDefaultBuilder(args)
					.UseStartup<Startup>()
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

		private static async Task AssertRpcNodeFullyInitializedAsync()
		{
			RPCResponse blockchainInfo = await Global.RpcClient.SendCommandAsync(RPCOperations.getblockchaininfo);
			try
			{				
				if (blockchainInfo.Error != null)
				{
					throw new NotSupportedException("blockchainInfo.Error != null");
				}
				if (blockchainInfo.Result == null)
				{
					throw new NotSupportedException("blockchainInfo.Result == null");
				}
				int blocks = blockchainInfo.Result.Value<int>("blocks");
				if (blocks == 0)
				{
					throw new NotSupportedException("blocks == 0");
				}
				int headers = blockchainInfo.Result.Value<int>("headers");
				if (headers == 0)
				{
					throw new NotSupportedException("headers == 0");
				}
				if (blocks != headers)
				{
					throw new NotSupportedException("blocks != headers");
				}

				if (await Global.RpcClient.SendCommandAsync("estimatesmartfee", 1, "ECONOMICAL") == null)
				{
					throw new NotSupportedException("estimatesmartfee 1 ECONOMICAL == null");
				}
			}
			catch
			{
				Console.WriteLine("Bitcoin Core is not yet fully initialized.");
				throw;
			}
		}
	}
}
