using WalletWasabi.Gui.ViewModels;
using ReactiveUI;
using Avalonia;
using System;
using System.Collections.Generic;
using Avalonia.Threading;
using WalletWasabi.Gui.ViewModels.Validation;
using System.Net;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.IO;

namespace WalletWasabi.Gui.Tabs
{
	internal class SettingsViewModel : WasabiDocumentTabViewModel
	{
		private string _network;
		private string _torHost;
		private string _torPort;
		private bool _isModified;

		public SettingsViewModel() : base("Settings")
		{
			var config = new Config(Global.Config.FilePath);

			this.WhenAnyValue(x => x.Network, x => x.TorHost, x => x.TorPort).Subscribe(x => Save());

			Dispatcher.UIThread.PostLogException(async () =>
			{
				await config.LoadFileAsync();

				Network = config.Network.ToString();
				TorHost = config.TorHost;
				TorPort = config.TorSocks5Port.ToString();

				IsModified = await Global.Config.CheckFileChangeAsync();
			});

			OpenConfigFileCommand = ReactiveCommand.Create(OpenConfigFile);
		}

		public IEnumerable<string> Networks
		{
			get
			{
				return new[]{
					  "Main"
					, "TestNet"
					, "RegTest"
				};
			}
		}

		public string Network
		{
			get { return _network; }
			set { this.RaiseAndSetIfChanged(ref _network, value); }
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

		public bool IsModified
		{
			get { return _isModified; }
			set { this.RaiseAndSetIfChanged(ref _isModified, value); }
		}

		private void Save()
		{
			var isValid = string.IsNullOrEmpty(ValidateTorHost()) &&
							string.IsNullOrEmpty(ValidateTorPort());
			if (!isValid) return;
			if (string.IsNullOrWhiteSpace(Network)) return;

			var config = new Config(Global.Config.FilePath);

			Dispatcher.UIThread.PostLogException(async () =>
			{
				await config.LoadFileAsync();

				var network = NBitcoin.Network.GetNetwork(Network);
				var torHost = TorHost;
				var torSocks5Port = int.TryParse(TorPort, out var port) ? (int?)port : null;

				if (config.Network != network || config.TorHost != torHost || config.TorSocks5Port != torSocks5Port)
				{
					config.Network = network;
					config.TorHost = torHost;
					config.TorSocks5Port = torSocks5Port;

					await config.ToFileAsync();

					IsModified = await Global.Config.CheckFileChangeAsync();
				}
			});
		}

		public string ValidateTorHost()
		{
			if (string.IsNullOrWhiteSpace(TorHost))
			{
				return string.Empty;
			}

			var torHost = TorHost.Trim();
			if (Uri.TryCreate(torHost, UriKind.Absolute, out var uri))
			{
				return string.Empty;
			}
			if (IPAddress.TryParse(torHost, out var ip))
			{
				if (ip.AddressFamily == AddressFamily.InterNetworkV6 && !Socket.OSSupportsIPv6)
				{
					return "OS does not support IPv6 addresses.";
				}
				return string.Empty;
			}

			return "Invalid host.";
		}

		public string ValidateTorPort()
		{
			if (string.IsNullOrEmpty(TorPort))
			{
				return string.Empty;
			}

			var torPort = TorPort.Trim();
			if (ushort.TryParse(torPort, out var port))
			{
				return string.Empty;
			}

			return "Invalid port.";
		}

		public ReactiveCommand OpenConfigFileCommand { get; }

		private void OpenConfigFile()
		{
			IoHelpers.OpenFileInTextEditor(Global.Config.FilePath);
		}
	}
}
