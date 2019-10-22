using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using NBitcoin.RPC;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace WalletWasabi.Tests.NodeBuilding
{
	public class CoreNode
	{
		private NodeBuilder Builder { get; }
		public string Folder { get; }

		public int P2pPort { get; }

		public EndPoint P2pEndPoint { get; }
		public EndPoint RpcEndPoint { get; }
		public RPCClient RpcClient { get; }
		public string Config { get; }

		public NodeConfigParameters ConfigParameters { get; } = new NodeConfigParameters();

		private CoreNode(string folder, NodeBuilder builder)
		{
			Builder = builder;
			Folder = folder;
			DataDir = Path.Combine(folder, "data");
			Directory.CreateDirectory(DataDir);
			var pass = Encoders.Hex.EncodeData(RandomUtils.GetBytes(20));
			var creds = new NetworkCredential(pass, pass);
			Config = Path.Combine(DataDir, "bitcoin.conf");
			ConfigParameters.Import(builder.ConfigParameters);

			var portArray = new int[2];
			var i = 0;
			while (i < portArray.Length)
			{
				var port = RandomUtils.GetUInt32() % 4000;
				port += 10000;
				if (portArray.Any(p => p == port))
				{
					continue;
				}

				try
				{
					var listener = new TcpListener(IPAddress.Loopback, (int)port);
					listener.Start();
					listener.Stop();
					portArray[i] = (int)port;
					i++;
				}
				catch (SocketException)
				{
				}
			}

			P2pPort = portArray[0];
			var rpcPort = portArray[1];
			P2pEndPoint = new IPEndPoint(IPAddress.Loopback, P2pPort);
			RpcEndPoint = new IPEndPoint(IPAddress.Loopback, rpcPort);

			RpcClient = new RPCClient($"{creds.UserName}:{creds.Password}", RpcEndPoint.ToString(rpcPort), Network.RegTest);
		}

		public async Task<IEnumerable<Block>> GenerateAsync(int blockCount)
		{
			var blocks = await RpcClient.GenerateAsync(blockCount);
			var rpc = RpcClient.PrepareBatch();
			var tasks = blocks.Select(b => rpc.GetBlockAsync(b));
			rpc.SendBatch();
			return await Task.WhenAll(tasks);
		}

		public static async Task<CoreNode> CreateAsync(string folder, NodeBuilder builder)
		{
			await IoHelpers.DeleteRecursivelyWithMagicDustAsync(folder);
			Directory.CreateDirectory(folder);

			return new CoreNode(folder, builder);
		}

		public async Task<Node> CreateNodeClientAsync()
		{
			return await Node.ConnectAsync(Network.RegTest, P2pEndPoint);
		}

		public async Task StartAsync()
		{
			var config = new NodeConfigParameters
			{
				{"regtest", "1"},
				{"regtest.rest", "1"},
				{"regtest.listenonion", "0"},
				{"regtest.server", "1"},
				{"regtest.txindex", "1"},
				{"regtest.rpcuser", RpcClient.CredentialString.UserPassword.UserName},
				{"regtest.rpcpassword", RpcClient.CredentialString.UserPassword.Password},
				{"regtest.whitebind", "127.0.0.1:" + P2pPort.ToString()},
				{"regtest.rpcport", RpcEndPoint.GetPortOrDefault().ToString()},
				{"regtest.printtoconsole", "0"}, // Set it to one if do not mind loud debug logs
				{"regtest.keypool", "10"},
				{"regtest.pid", "bitcoind.pid"}
			};
			config.Import(ConfigParameters);
			await File.WriteAllTextAsync(Config, config.ToString());
			using (await KillerLock.LockAsync())
			{
				Process = Process.Start(new FileInfo(Builder.BitcoinD).FullName, "-conf=bitcoin.conf" + " -datadir=" + DataDir + " -debug=1");
				string pidFile = Path.Combine(DataDir, "regtest", "bitcoind.pid");
				if (!File.Exists(pidFile))
				{
					Directory.CreateDirectory(Path.Combine(DataDir, "regtest"));
					await File.WriteAllTextAsync(pidFile, Process.Id.ToString());
				}
			}
			while (true)
			{
				try
				{
					await RpcClient.GetBlockHashAsync(0);
					break;
				}
				catch
				{
				}
				if (Process is null || Process.HasExited)
				{
					break;
				}
			}
		}

		private Process Process { get; set; }
		private string DataDir { get; }

		private readonly AsyncLock KillerLock = new AsyncLock();

		public async Task TryKillAsync()
		{
			try
			{
				using (await KillerLock.LockAsync())
				{
					try
					{
						await RpcClient.StopAsync();
						if (!Process.WaitForExit(20000))
						{
							//log this
						}
					}
					catch (Exception)
					{ }
				}
				await IoHelpers.DeleteRecursivelyWithMagicDustAsync(Folder);
			}
			catch
			{ }
		}
	}
}
