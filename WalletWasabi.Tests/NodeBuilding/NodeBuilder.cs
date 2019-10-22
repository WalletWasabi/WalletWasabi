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
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Tests.XunitConfiguration;

namespace WalletWasabi.Tests.NodeBuilding
{
	public class NodeBuilder : IDisposable
	{
		public static string WorkingDirectory { get; private set; }

		public static NodeBuilder Create([CallerMemberName]string caller = null)
		{
			WorkingDirectory = Path.Combine(Global.Instance.DataDir, caller);
			return new NodeBuilder(WorkingDirectory, EnvironmentHelpers.GetBinaryPath("BitcoinCore", "bitcoind"));
		}

		private static async Task TryRemoveWorkingDirectoryAsync()
		{
			try
			{
				await IoHelpers.DeleteRecursivelyWithMagicDustAsync(WorkingDirectory);
			}
			catch (DirectoryNotFoundException)
			{
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}

		private int Last { get; set; }
		private string Root { get; }

		public NodeBuilder(string root, string bitcoindPath)
		{
			Root = root;
			BitcoinD = bitcoindPath;
		}

		public string BitcoinD { get; }
		public List<CoreNode> Nodes { get; } = new List<CoreNode>();
		public NodeConfigParameters ConfigParameters { get; } = new NodeConfigParameters();
		public Network Network => Network.RegTest;

		public async Task<CoreNode> CreateNodeAsync(bool start = false)
		{
			var child = Path.Combine(Root, Last.ToString());
			Last++;
			try
			{
				var cfgPath = Path.Combine(child, "data", "bitcoin.conf");
				if (File.Exists(cfgPath))
				{
					var config = NodeConfigParameters.Load(cfgPath);
					var rpcPort = config["regtest.rpcport"];
					var rpcUser = config["regtest.rpcuser"];
					var rpcPassword = config["regtest.rpcpassword"];
					var pidFileName = config["regtest.pid"];
					var credentials = new NetworkCredential(rpcUser, rpcPassword);
					try
					{
						var rpc = new RPCClient(credentials, new Uri("http://127.0.0.1:" + rpcPort + "/"), Network.RegTest);
						await rpc.StopAsync();

						var pidFile = Path.Combine(child, "data", "regtest", pidFileName);
						if (File.Exists(pidFile))
						{
							var pid = File.ReadAllText(pidFile);
							using var process = Process.GetProcessById(int.Parse(pid));
							process.WaitForExit();
						}
						else
						{
							var allProcesses = Process.GetProcesses();
							var bitcoindProcesses = allProcesses.Where(x => x.ProcessName.Contains("bitcoind"));
							if (bitcoindProcesses.Count() == 1)
							{
								var bitcoind = bitcoindProcesses.First();
								bitcoind.WaitForExit();
							}
						}
					}
					catch (Exception)
					{
					}
				}
				await IoHelpers.DeleteRecursivelyWithMagicDustAsync(child);
				await TryRemoveWorkingDirectoryAsync();
				Directory.CreateDirectory(WorkingDirectory);
			}
			catch (DirectoryNotFoundException)
			{
			}
			var node = await CoreNode.CreateAsync(child, this);
			Nodes.Add(node);
			if (start)
			{
				await node.StartAsync();
			}
			return node;
		}

		public Task StartAllAsync()
		{
			var startNodesTaskList = Nodes
				.Where(n => n.State == CoreNodeState.Stopped)
				.Select(n => n.StartAsync()).ToArray();
			return Task.WhenAll(startNodesTaskList);
		}

		public void Dispose()
		{
			foreach (CoreNode node in Nodes)
			{
				node?.TryKillAsync().GetAwaiter().GetResult();
			}

			TryRemoveWorkingDirectoryAsync().GetAwaiter().GetResult();
		}
	}
}
