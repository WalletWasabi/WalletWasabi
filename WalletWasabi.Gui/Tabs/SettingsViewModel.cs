using WalletWasabi.Gui.ViewModels;
using ReactiveUI;
using Avalonia;
using System;
using System.Collections.Generic;
using Avalonia.Threading;

namespace WalletWasabi.Gui.Tabs
{
	internal class SettingsViewModel : WasabiDocumentTabViewModel
	{
		private string _network;

		public SettingsViewModel() : base("Settings")
		{
			_network = Global.Config.Network.Name == "Main" ? "MainNet" : "TestNet3";
		}

		public IEnumerable<string> Networks
		{
			get
			{
				return new[]{
					  "MainNet"
					, "TestNet3"
#if DEBUG
					, "RegTest"
#endif
				};
			}
		}

		public string Network
		{
			get { return _network; }
			set
			{
				if (value == _network) return;

				var config = Global.Config.Clone();
				config.Network = NBitcoin.Network.GetNetwork(value);
				Dispatcher.UIThread.Post(async () =>
				{
					await config.ToFileAsync();
				});

				this.RaiseAndSetIfChanged(ref _network, value); 
			}
		}
	}
}
