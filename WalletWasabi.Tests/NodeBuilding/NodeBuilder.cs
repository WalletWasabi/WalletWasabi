using Microsoft.EntityFrameworkCore.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WalletWasabi.Tests.XunitConfiguration;

namespace WalletWasabi.Tests.NodeBuilding
{
	public class NodeBuilder : IDisposable
	{
		public static readonly AsyncLock Lock = new AsyncLock();

		public static async Task<NodeBuilder> CreateAsync([CallerMemberName]string caller = null, string version = "0.16.3")
		{
			using (await Lock.LockAsync())
			{
				var directory = Path.Combine(SharedFixture.DataDir, caller);
				version = version ?? "0.16.3";
				var path = await EnsureDownloadedAsync(version);
				try
				{
					await IoHelpers.DeleteRecursivelyWithMagicDustAsync(directory);
				}
				catch (DirectoryNotFoundException)
				{
				}
				Directory.CreateDirectory(directory);
				return new NodeBuilder(directory, path);
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
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				bitcoind = Path.Combine(SharedFixture.DataDir, $"bitcoin-{version}", "bin", "bitcoind.exe");
				if (File.Exists(bitcoind))
					return bitcoind;
				zip = Path.Combine(SharedFixture.DataDir, $"bitcoin-{version}-win32.zip");
				var url = string.Format("https://bitcoincore.org/bin/bitcoin-core-{0}/" + Path.GetFileName(zip), version);
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
				bitcoind = Path.Combine(SharedFixture.DataDir, $"bitcoin-{version}", "bin", "bitcoind");
				if (File.Exists(bitcoind))
					return bitcoind;

				zip = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ?
					Path.Combine(SharedFixture.DataDir, $"bitcoin-{version}-x86_64-linux-gnu.tar.gz")
					: Path.Combine(SharedFixture.DataDir, $"bitcoin-{version}-osx64.tar.gz");

				var url = string.Format("https://bitcoincore.org/bin/bitcoin-core-{0}/" + Path.GetFileName(zip), version);
				using (var client = new HttpClient())
				{
					client.Timeout = TimeSpan.FromMinutes(10.0);
					var data = await client.GetByteArrayAsync(url);
					await File.WriteAllBytesAsync(zip, data);

					Process.Start("tar", "-zxvf " + zip + " -C " + SharedFixture.DataDir).WaitForExit();
				}
			}
			File.Delete(zip);
			return bitcoind;
		}

		private int _last = 0;
		private readonly string _root;

		public NodeBuilder(string root, string bitcoindPath)
		{
			_root = root;
			BitcoinD = bitcoindPath;
		}

		public string BitcoinD { get; }
		public List<CoreNode> Nodes { get; } = new List<CoreNode>();
		public NodeConfigParameters ConfigParameters { get; } = new NodeConfigParameters();

		public async Task<CoreNode> CreateNodeAsync(bool start = false)
		{
			var child = Path.Combine(_root, _last.ToString());
			_last++;
			try
			{
				await IoHelpers.DeleteRecursivelyWithMagicDustAsync(child);
			}
			catch (DirectoryNotFoundException)
			{
			}
			var node = new CoreNode(child, this);
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
			foreach (var node in Nodes)
				node.Kill();
			foreach (var disposable in _disposables)
				disposable.Dispose();
		}

		private List<IDisposable> _disposables = new List<IDisposable>();

		internal void AddDisposable(IDisposable group)
		{
			_disposables.Add(group);
		}
	}
}
