using NBitcoin;
using NBitcoin.Protocol;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Gui.ViewModels;
using Global = WalletWasabi.Gui.Global;

namespace WalletWasabi.Fluent.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
		private Global _global;
		private StatusBarViewModel _statusBar;
		private string _title = "Wasabi Wallet";

		public static MainViewModel Instance { get; internal set; }

		public MainViewModel(Global global)
		{
			_global = global;

			Network = global.Network;

			StatusBar = new StatusBarViewModel(global.DataDir, global.Network, global.Config, global.HostedServices, global.BitcoinStore.SmartHeaderChain, global.Synchronizer, global.LegalDocuments);
		}

		public void Initialize()
		{
			StatusBar.Initialize(_global.Nodes.ConnectedNodes);

			if (Network != Network.Main)
			{
				Title += $" - {Network}";
			}
		}

		public Network Network { get; }		

		public StatusBarViewModel StatusBar
		{
			get { return _statusBar; }
			set { this.RaiseAndSetIfChanged(ref _statusBar, value); }
		}

		public string Title
		{
			get => _title;
			internal set => this.RaiseAndSetIfChanged(ref _title, value);
		}
	}
}
