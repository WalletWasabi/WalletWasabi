using WalletWasabi.Gui.ViewModels;
using ReactiveUI;
using Avalonia;
using System;
using System.Collections.Generic;
using Avalonia.Threading;
using WalletWasabi.Gui.ViewModels.Validation;
using System.Net;

namespace WalletWasabi.Gui.Tabs
{
	internal class SettingsViewModel : WasabiDocumentTabViewModel
	{
		private string _network;
		private string _torHost;
		private string _torPort;

		public SettingsViewModel() : base("Settings")
		{
			_network = Global.Config.Network.Name == "Main" ? "MainNet" : "TestNet";
			_torHost = Global.Config.TorHost;
			_torPort = Global.Config.TorSocks5Port.HasValue 
				? Global.Config.TorSocks5Port.Value.ToString() 
				: string.Empty;

			this.WhenAnyValue(x=>x.Network, x=>x.TorHost, x=>x.TorPort).Subscribe(x=>Save());
		}

		public IEnumerable<string> Networks
		{
			get
			{
				return new[]{
					  "MainNet"
					, "TestNet"
#if DEBUG
					, "RegTest"
#endif
				};
			}
		}

		public string Network
		{
			get { return _network; }
			set	{ this.RaiseAndSetIfChanged(ref _network, value); }
		}

		[ValidateMethod(nameof(ValidateTorHost))]
		public string TorHost
		{
			get { return _torHost; }
			set { this.RaiseAndSetIfChanged(ref _torHost, value); }
		}

		[ValidateMethod(nameof(ValidateTorPort))]
		public string TorPort
		{
			get { return _torPort; }
			set { this.RaiseAndSetIfChanged(ref _torPort, value); }
		}

		private void Save()
		{
			var isValid  =  string.IsNullOrEmpty(ValidateTorHost()) &&
							string.IsNullOrEmpty(ValidateTorPort());  
			if(!isValid) return;

			var config = Global.Config.Clone();
			config.Network = NBitcoin.Network.GetNetwork(_network);
			config.TorHost = _torHost;
			config.TorSocks5Port = int.TryParse(_torPort, out var port) ? (int?)port : null;

			Dispatcher.UIThread.Post(async () =>
			{
				await config.ToFileAsync();
			});
		}

		public string ValidateTorHost()
		{
			if (!string.IsNullOrWhiteSpace(TorHost))
			{
				var torHost = TorHost.Trim();
				if(Uri.TryCreate(torHost, UriKind.Absolute, out var uri))
				{
					return string.Empty;
				}
				if(IPAddress.TryParse(torHost, out var ip))
				{
					return string.Empty;
				}
			}

			return "Host is not valid";
		}

		public string ValidateTorPort()
		{
			if (string.IsNullOrEmpty(TorPort))
			{
				return string.Empty;
			}

			var torPort = TorPort.Trim();
			if(ushort.TryParse(torPort, out var port))
			{
				return string.Empty;
			}

			return "Port is not valid";
		}
	}
}
