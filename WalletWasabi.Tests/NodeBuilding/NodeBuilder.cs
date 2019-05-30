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
using WalletWasabi.Logging;
using WalletWasabi.Tests.XunitConfiguration;

namespace WalletWasabi.Tests.NodeBuilding
{
	public class NodeBuilder : IDisposable
	{
		public static readonly AsyncLock Lock = new AsyncLock();
		public static string WorkingDirectory { get; private set; }

		public static async Task<NodeBuilder> CreateAsync([CallerMemberName]string caller = null, string version = "0.18.0")
		{
			using (await Lock.LockAsync())
			{
				WorkingDirectory = Path.Combine(Global.DataDir, caller);
				version = version ?? "0.18.0";
				var path = await EnsureDownloadedAsync(version);
				return new NodeBuilder(WorkingDirectory, path);
			}
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
				Logger.LogError<NodeBuilder>(ex);
			}
		}

		private static async Task<string> EnsureDownloadedAsync(string version)
		{
			//is a file
			if (version.Length >= 2 && version[1] == ':')
			{
				return version;
			}

			string zip;
			string bitcoind;
			string bitcoindFolderName = $"bitcoin-{version}";

			// Remove old bitcoind folders.
			IEnumerable<string> existingBitcoindFolderPaths = Directory.EnumerateDirectories(Global.DataDir, "bitcoin-*", SearchOption.TopDirectoryOnly);
			foreach (string dirPath in existingBitcoindFolderPaths)
			{
				string dirName = Path.GetFileName(dirPath);
				if (bitcoindFolderName != dirName)
				{
					await IoHelpers.DeleteRecursivelyWithMagicDustAsync(dirPath);
				}
			}

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				bitcoind = Path.Combine(Global.DataDir, bitcoindFolderName, "bin", "bitcoind.exe");
				if (File.Exists(bitcoind))
				{
					return bitcoind;
				}

				zip = Path.Combine(Global.DataDir, $"bitcoin-{version}-win64.zip");
				string url = string.Format("https://bitcoincore.org/bin/bitcoin-core-{0}/" + Path.GetFileName(zip), version);
				using (var client = new HttpClient())
				{
					client.Timeout = TimeSpan.FromMinutes(10.0);
					var data = await client.GetByteArrayAsync(url);
					await File.WriteAllBytesAsync(zip, data);
					ZipFile.ExtractToDirectory(zip, new FileInfo(zip).Directory.FullName);
				}
			}
			else
			{
				bitcoind = Path.Combine(Global.DataDir, bitcoindFolderName, "bin", "bitcoind");
				if (File.Exists(bitcoind))
				{
					return bitcoind;
				}

				zip = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ?
					Path.Combine(Global.DataDir, $"bitcoin-{version}-x86_64-linux-gnu.tar.gz")
					: Path.Combine(Global.DataDir, $"bitcoin-{version}-osx64.tar.gz");

				string url = string.Format("https://bitcoincore.org/bin/bitcoin-core-{0}/" + Path.GetFileName(zip), version);
				using (var client = new HttpClient())
				{
					client.Timeout = TimeSpan.FromMinutes(10.0);
					var data = await client.GetByteArrayAsync(url);
					await File.WriteAllBytesAsync(zip, data);

					using (var process = Process.Start("tar", "-zxvf " + zip + " -C " + Global.DataDir))
					{
						process.WaitForExit();
					}
				}
			}
			File.Delete(zip);
			return bitcoind;
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
							using (var process = Process.GetProcessById(int.Parse(pid)))
							{
								process.WaitForExit();
							}
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

			foreach (IDisposable disposable in Disposables)
			{
				disposable?.Dispose();
			}

			TryRemoveWorkingDirectoryAsync().GetAwaiter().GetResult();
		}

		private List<IDisposable> Disposables { get; } = new List<IDisposable>();

		internal void AddDisposable(IDisposable group)
		{
			Disposables.Add(group);
		}
	}
}
