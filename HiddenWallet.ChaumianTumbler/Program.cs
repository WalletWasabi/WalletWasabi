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
				var configFilePath = Path.Combine(Global.DataDir, "Config.json");
				Global.Config = new Config();
				await Global.Config.LoadOrCreateDefaultFileAsync(configFilePath, CancellationToken.None);

				Global.RpcClient = new RPCClient(
					credentials: new RPCCredentialString
					{
						UserPassword = new NetworkCredential(Global.Config.BitcoinRpcUser, Global.Config.BitcoinRpcPassword)
					},
					network: Global.Config.Network);
				await AssertRpcNodeFullyInitializedAsync();

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

				if (await Global.RpcClient.EstimateFeeRateAsync(1) == null)
				{
					throw new NotSupportedException("estimatefee 1 == null");
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
