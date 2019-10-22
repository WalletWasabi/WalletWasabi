using NBitcoin;
using NBitcoin.RPC;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Tests.XunitConfiguration;

namespace WalletWasabi.Tests.NodeBuilding
{
	public static class NodeBuilder
	{
		public static async Task<CoreNode> CreateNodeAsync([CallerMemberName]string caller = null)
		{
			var dataDir = Path.Combine(Global.Instance.DataDir, caller);

			var cfgPath = Path.Combine(dataDir, "bitcoin.conf");
			if (File.Exists(cfgPath))
			{
				var config = await NodeConfigParameters.LoadAsync(cfgPath);
				var rpcPort = config["regtest.rpcport"];
				var rpcUser = config["regtest.rpcuser"];
				var rpcPassword = config["regtest.rpcpassword"];
				var pidFileName = config["regtest.pid"];
				var credentials = new NetworkCredential(rpcUser, rpcPassword);
				try
				{
					var rpc = new RPCClient(credentials, new Uri("http://127.0.0.1:" + rpcPort + "/"), Network.RegTest);
					await rpc.StopAsync();

					var pidFile = Path.Combine(dataDir, "regtest", pidFileName);
					if (File.Exists(pidFile))
					{
						var pid = await File.ReadAllTextAsync(pidFile);
						using var process = Process.GetProcessById(int.Parse(pid));
						await process.WaitForExitAsync(CancellationToken.None);
					}
					else
					{
						var allProcesses = Process.GetProcesses();
						var bitcoindProcesses = allProcesses.Where(x => x.ProcessName.Contains("bitcoind"));
						if (bitcoindProcesses.Count() == 1)
						{
							var bitcoind = bitcoindProcesses.First();
							await bitcoind.WaitForExitAsync(CancellationToken.None);
						}
					}
				}
				catch (Exception)
				{
				}
			}

			await IoHelpers.DeleteRecursivelyWithMagicDustAsync(dataDir);
			var node = new CoreNode(dataDir);
			await node.StartAsync();
			return node;
		}
	}
}
