using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MagicalCryptoWallet.Tests.NodeBuilding
{
	public class NodeBuilder : IDisposable
	{
		public static NodeBuilder Create([CallerMemberName]string caller = null, string version = "0.16.0")
		{
			var directory = Path.Combine(SharedFixture.DataDir, caller);
			version = version ?? "0.16.0";
			var path = EnsureDownloaded(version);
			try
			{
				IoHelpers.DeleteRecursivelyWithMagicDust(directory);
			}
			catch (DirectoryNotFoundException)
			{
			}
			Directory.CreateDirectory(directory);
			return new NodeBuilder(directory, path);
		}

		private static string EnsureDownloaded(string version)
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
				bitcoind = Path.Combine(SharedFixture.DataDir,$"bitcoin-{version}", "bin", "bitcoind.exe");
				if (File.Exists(bitcoind))
					return bitcoind;
				zip = Path.Combine(SharedFixture.DataDir, $"bitcoin-{version}-win32.zip");
				string url = string.Format("https://bitcoin.org/bin/bitcoin-core-{0}/" + Path.GetFileName(zip), version);
				HttpClient client = new HttpClient();
				client.Timeout = TimeSpan.FromMinutes(10.0);
				var data = client.GetByteArrayAsync(url).GetAwaiter().GetResult();
				File.WriteAllBytes(zip, data);
				ZipFile.ExtractToDirectory(zip, new FileInfo(zip).Directory.FullName);
			}
			else
			{
				bitcoind = Path.Combine(SharedFixture.DataDir, $"bitcoin-{version}", "bin", "bitcoind");
				if (File.Exists(bitcoind))
					return bitcoind;

				zip = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ?
					Path.Combine(SharedFixture.DataDir, $"bitcoin-{version}-x86_64-linux-gnu.tar.gz")
					: Path.Combine(SharedFixture.DataDir, $"bitcoin-{version}-osx64.tar.gz");

				string url = string.Format("https://bitcoin.org/bin/bitcoin-core-{0}/" + Path.GetFileName(zip), version);
				HttpClient client = new HttpClient();
				client.Timeout = TimeSpan.FromMinutes(10.0);
				var data = client.GetByteArrayAsync(url).GetAwaiter().GetResult();
				File.WriteAllBytes(zip, data);
				Process.Start("tar", "-zxvf " + zip + " -C " + SharedFixture.DataDir).WaitForExit();
			}
			File.Delete(zip);
			return bitcoind;
		}

		private int _last = 0;
		private string _root;
		private string _bitcoind;
		public NodeBuilder(string root, string bitcoindPath)
		{
			_root = root;
			_bitcoind = bitcoindPath;
		}

		public string BitcoinD
		{
			get
			{
				return _bitcoind;
			}
		}


		private readonly List<CoreNode> _Nodes = new List<CoreNode>();
		public List<CoreNode> Nodes
		{
			get
			{
				return _Nodes;
			}
		}


		private readonly NodeConfigParameters _ConfigParameters = new NodeConfigParameters();
		public NodeConfigParameters ConfigParameters
		{
			get
			{
				return _ConfigParameters;
			}
		}

		public CoreNode CreateNode(bool start = false)
		{
			var child = Path.Combine(_root, _last.ToString());
			_last++;
			try
			{
				IoHelpers.DeleteRecursivelyWithMagicDust(child);
			}
			catch (DirectoryNotFoundException)
			{
			}
			var node = new CoreNode(child, this);
			Nodes.Add(node);
			if (start)
				node.Start();
			return node;
		}

		public void StartAll()
		{
			Task.WaitAll(Nodes.Where(n => n.State == CoreNodeState.Stopped).Select(n => n.StartAsync()).ToArray());
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