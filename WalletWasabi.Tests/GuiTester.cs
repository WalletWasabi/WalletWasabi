using NBitcoin.Tests;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Gui;
using System.Net.Sockets;
using System.Net;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using WalletWasabi.Backend;
using System.Threading;
using NBitcoin;
using WalletWasabi.Models.ChaumianCoinJoin;

namespace WalletWasabi.Tests
{
	public class GuiTester : IDisposable
	{
		List<GuiClientTester> _guiClients = new List<GuiClientTester>();
		Stack<IDisposable> _resources = new Stack<IDisposable>();
		private readonly NodeBuilder _nodeBuilder;
		public NodeBuilder NodeBuilder
		{
			get
			{
				return _nodeBuilder;
			}
		}

		private readonly string _testName;
		public string TestName
		{
			get
			{
				return _testName;
			}
		}

		public Uri BackendUri { get; internal set; }
		public IWebHost BackendHost { get; private set; }

		public static GuiTester Create([CallerMemberName] string testName = null)
		{
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
			TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
			return new GuiTester(testName);
		}

		private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
		{
			e.Exception.ToString();
		}

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
		}

		private GuiTester(string testName)
		{
			_testName = testName;
			IoHelpers.DeleteRecursivelyWithMagicDustAsync(_testName).GetAwaiter().GetResult();
			_nodeBuilder = NBitcoin.Tests.NodeBuilder.Create(NodeDownloadData.Bitcoin.v0_18_0, NBitcoin.Network.RegTest, _testName);
			_resources.Push(_nodeBuilder);
		}

		public T GetBackendService<T>()
		{
			return (T)BackendHost.Services.GetService(typeof(T));
		}

		public GuiClientTester CreateGuiClient()
		{
			string guiDatadir = Path.Combine(TestName, "GuiClients", _guiClients.Count.ToString());
			var child = new GuiClientTester(this, guiDatadir);
			_guiClients.Add(child);
			_resources.Push(child);
			return child;
		}

		public CoreNode BackendNode { get; set; }

		internal bool _started;
		public async Task StartBackendAsync()
		{
			if (_started)
			{
				return;
			}

			BackendNode = _nodeBuilder.CreateNode();
			BackendNode.WhiteBind = true;
			await BackendNode.StartAsync();
			_resources.Push(BackendNode.AsDisposable());
			var rpc = BackendNode.CreateRPCClient();
			var config = new Backend.Config(rpc.Network, rpc.Authentication, IPAddress.Loopback.ToString(), IPAddress.Loopback.ToString(), BackendNode.Endpoint.Address.ToString(), Network.Main.DefaultPort, Network.TestNet.DefaultPort, BackendNode.Endpoint.Port);
			var roundConfig = new CcjRoundConfig(Money.Coins(0.1m), WalletWasabi.Helpers.Constants.OneDayConfirmationTarget, 0.7, 0.1m, 100, 120, 60, 60, 60, 1, 24, true, 11);
			await Backend.Global.Instance.InitializeAsync(config, roundConfig, rpc);

			BackendUri = new Uri($"http://127.0.0.1:{FreeTcpPort()}/", UriKind.Absolute);
			BackendHost = WebHost.CreateDefaultBuilder()
					.UseStartup<Startup>()
					.UseUrls(BackendUri.AbsoluteUri)
					.Build();
			_resources.Push(BackendHost);

			_ = BackendHost.RunAsync();
			var started = GetBackendService<IApplicationLifetime>().ApplicationStarted;
			try
			{
				await Task.Delay(10_000, started);
				throw new OperationCanceledException("The backend did not started in 10s");
			}
			catch when (started.IsCancellationRequested) { }
			_started = true;
		}

		static int FreeTcpPort()
		{
			TcpListener l = new TcpListener(IPAddress.Loopback, 0);
			l.Start();
			int port = ((IPEndPoint)l.LocalEndpoint).Port;
			l.Stop();
			return port;
		}

		public async Task StartAllAsync()
		{
			await StartBackendAsync();
			foreach (var gui in _guiClients.Select(g => g.StartAsync()).ToArray())
			{
				await gui;
			}
		}

		public void Dispose()
		{
			while (_resources.TryPop(out var v))
			{
				v.Dispose();
			}
		}
	}

	public class GuiClientTester : IDisposable
	{
		private GuiTester _parent;
		private string _dataDir;

		public GuiClientTester(GuiTester parent, string datadir)
		{
			_parent = parent;
			_dataDir = datadir;
			_node = parent.NodeBuilder.CreateNode();
			_GuiGlobal = new Gui.Global(datadir);
		}


		private readonly CoreNode _node;
		public CoreNode Node
		{
			get
			{
				return _node;
			}
		}


		private readonly WalletWasabi.Gui.Global _GuiGlobal;
		public WalletWasabi.Gui.Global GuiGlobal
		{
			get
			{
				return _GuiGlobal;
			}
		}


		private MainWindowViewModel _mainViewModel;
		public MainWindowViewModel MainViewModel
		{
			get
			{
				return _mainViewModel;
			}
		}

		bool _started;
		public async Task StartAsync()
		{
			if (_started)
			{
				return;
			}
			if (!_parent._started)
			{
				throw new InvalidOperationException("The coinjoin node should start before the client nodes");
			}
			Node.ConfigParameters.Add("connect", $"{_parent.BackendNode.Endpoint.Address}:{_parent.BackendNode.Endpoint.Port}");
			Node.ConfigParameters.Add("listen", "1");
			await Node.StartAsync();
			_mainViewModel = new MainWindowViewModel { Global = GuiGlobal };

			var config = new Gui.Config(Path.Combine(_dataDir, "Config.json"));
			await config.LoadOrCreateDefaultFileAsync();
			config.Network = _parent.NodeBuilder.Network;
			config.RegTestBitcoinCorePort = _node.NodeEndpoint.Port;
			config.RegTestBitcoinCoreHost = _node.NodeEndpoint.Address.ToString();
			config.RegTestBackendUriV3 = _parent.BackendUri.AbsoluteUri;
			config.UseTor = false;
			await config.ToFileAsync();

			await GuiGlobal.InitializeNoWalletAsync();

			var statusBar = new StatusBarViewModel(GuiGlobal, _mainViewModel);
			statusBar.Initialize(GuiGlobal.Nodes.ConnectedNodes, GuiGlobal.Synchronizer, GuiGlobal.UpdateChecker);
			_started = true;
		}

		public void Dispose()
		{
			GuiGlobal.DisposeAsync().GetAwaiter().GetResult();
			_node.AsDisposable().Dispose();
		}
	}
}
