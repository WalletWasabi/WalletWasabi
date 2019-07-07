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
			_nodeBuilder = NBitcoin.Tests.NodeBuilder.Create(NodeDownloadData.Bitcoin.v0_18_0, NBitcoin.Network.RegTest, testName);
			_resources.Push(_nodeBuilder);
		}

		public GuiClientTester CreateGuiClient()
		{
			string guiDatadir = Path.Combine(TestName, "GuiClients", _guiClients.Count.ToString());
			var child = new GuiClientTester(this, guiDatadir);
			_guiClients.Add(child);
			_resources.Push(child);
			return child;
		}

		public async Task StartAllAsync()
		{
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

		public async Task StartAsync()
		{
			await Node.StartAsync();
			_mainViewModel = new MainWindowViewModel { Global = GuiGlobal };

			var config = new Config(Path.Combine(_dataDir, "Config.json"));
			await config.LoadOrCreateDefaultFileAsync();
			config.Network = _parent.NodeBuilder.Network;
			config.RegTestBitcoinCorePort = _node.NodeEndpoint.Port;
			config.RegTestBitcoinCoreHost = _node.NodeEndpoint.Address.ToString();
			config.UseTor = false;
			await config.ToFileAsync();

			await GuiGlobal.InitializeNoWalletAsync();

			var statusBar = new StatusBarViewModel(GuiGlobal, _mainViewModel);
			statusBar.Initialize(GuiGlobal.Nodes.ConnectedNodes, GuiGlobal.Synchronizer, GuiGlobal.UpdateChecker);
		}

		public void Dispose()
		{
			GuiGlobal.DisposeAsync().GetAwaiter().GetResult();
			_node.Kill();
			_node.WaitForExit();
		}
	}
}
